using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using xxxx.Api.Configuration;
using xxxx.Api.Entities;
using xxxx.Api.Logging;
using xxxx.Api.SharePointManagement;
using xxxx.Api.DocumentManagement.Document;
using xxxx.Api.DataAccess;

using xxxx.Api;
using xxxx.Api.DocumentManagement;

//Occupational Health Vision Data Processing
namespace xxxx.Import
{
    public class Importer
    {
        private IRepositoryFactory ohvRepositoryFactory;

        //private SharePointInteractionFactory sharePointFactory;

        private IDocumentManager locDocumentManager;

        private Uri siteUrl;

        private DocumentFolderManager folderManager;

        private int documentsToBuffer;

        private int documentsToProcess;

        private int numberOfThreads;

        private Semaphore semaphore;

        private bool abortThreads;

        private IEnumerable<Role> roles;

        public Importer( IDocumentManager inDocumentManager,IRepositoryFactory ohvRepositoryFactory, DocumentFolderManager folderManager, Uri siteUrl, int documentsToBuffer, int documentsToProcess, int numberOfThreads)
        {
            this.locDocumentManager = inDocumentManager;
            this.siteUrl = siteUrl;
            this.folderManager = folderManager;
            this.documentsToBuffer = documentsToBuffer;
            this.documentsToProcess = documentsToProcess;
            this.ohvRepositoryFactory = ohvRepositoryFactory;
            this.numberOfThreads = numberOfThreads;

            using (IContentServiceRepository ohvRepository = ohvRepositoryFactory.CreateInstance())
            {
                this.roles = ohvRepository.GetAllRoles().ToList();
            }
        }

        private DocumentBase GenerateDocumentFromImportItem(ImportItem importRecord)
        {
            DocumentBase result;

            if (importRecord.MessageId == 0)
            {
                result = new RtaDocument()
                {
                    ContractName = importRecord.ContractName,
                    ContractClientId = importRecord.ClientId.ToString(),
                    Surname = importRecord.Surname,
                    Forenames = importRecord.Forenames,
                    DateOfBirth = importRecord.DateOfBirth,
                    FileType = importRecord.FileType,
                    DocumentType = importRecord.DocumentType,
                    ReferralId = importRecord.ReferralId.ToString(),
                    WithholdFromClient = importRecord.WithholdFromClient,
                    SubContract = importRecord.SubContract,
                    Void = importRecord.Void,
                    Roles = roles.Where(r => r.DocumentClass == importRecord.Class).Select(r => r.ExternalGroupName).ToList<string>()
                };
            }
            else
            {
                result = new MsnDocument()
                {
                    MessageId = importRecord.MessageId.ToString()
                };
            }

            result.DateOnDocument = importRecord.DateOnDocument;
            result.FileName = importRecord.FileName;
            result.RTFContent = Convert.ToBase64String(File.ReadAllBytes(importRecord.FilePath));
            result.Title = importRecord.DocumentTitle;
            result.Class = importRecord.Class;
            result.MimeType = importRecord.MimeType;

            // We generate our own id usually, but for tracability with the orignal
            // in FileNET we'll use the one provided.
            result.Id = importRecord.Id;
            return result;
        }

        private List<DocumentBase> GenerateDocumentsFromImportItems(IEnumerable<ImportItem> importRecords)
        {
            List<DocumentBase> documents = new List<DocumentBase>();

            try
            {

                foreach (ImportItem item in importRecords)
                {
                    documents.Add(GenerateDocumentFromImportItem(item));
                }
            }
            catch (Exception ex)
            {
                Report("Error building block document block from import records. No rollback necessary. Error: " + ex.Message);
                throw new Exception("Error building block document block from import records. No rollback necessary. Error: " + ex.Message);
            }

            return documents;
        }

        private void EnforceUniqueFileName(List<DocumentBase> documents, HashSet<string> fileNameHash)
        {
            foreach (DocumentBase document in documents)
            {
                while (!fileNameHash.Add(document.FileName))
                {
                    document.FileNamePrefix++;
                }
            }
        }

        private List<IEnumerable<ImportItem>> CreateBlocks(List<ImportItem> importItems, int blockSize)
        {
            List<IEnumerable<ImportItem>> importItemBlocks = new List<IEnumerable<ImportItem>>();
            while (importItems.Any())
            {
                importItemBlocks.Add(importItems.Take(blockSize).ToList());
                importItems = importItems.Skip(blockSize).ToList();
            }

            return importItemBlocks;
        }

        private void MarkImported(List<ImportItem> importItems)
        {
            foreach (ImportItem item in importItems)
            {
                item.Imported = true;
            }
        }



        private CommitResult CheckDocumentsAndSendToSharePoint(List<DocumentBase> documents, HashSet<string> fileNameHash)
        {
            EnforceUniqueFileName(documents, fileNameHash);
            CommitResult result = locDocumentManager.AddDocuments(documents);
            
            return result;


        }

        private void Rollback(List<DocumentBase> documentBlock)
        {
            int rollbackCount = 0;
            List<Guid> ids = (from documents in documentBlock
                              select documents.Id).ToList();

            //sharePoint.DeleteDocuments(ids, out rollbackCount);
            locDocumentManager.RemoveDocuments(ids);
        }

        private void IdentifyAndFixFileName(List<DocumentBase> documentBlock, string errorMessage)
        {
            foreach (DocumentBase document in documentBlock)
            {
                if (errorMessage.Contains(document.FileName))
                {
                    document.FileNamePrefix++;
                }
            }
        }


        private bool TryDocumentAdditon(List<DocumentBase> documentBlock, HashSet<string> fileNameHash)
        {
            //
            CommitResult commitResult = new CommitResult("Starting Addition To SharePoint.");
            bool blnTryDocumentAddition = true;
            while (blnTryDocumentAddition)
            {

                commitResult = CheckDocumentsAndSendToSharePoint(documentBlock, fileNameHash);
                if (!commitResult.Success && commitResult.ErrorCode == OhvConfiguration.MaxValueErrorCode)
                {
                    Rollback(documentBlock);
                    IdentifyAndFixFileName(documentBlock, commitResult.ErrorMessage);
                }
                else
                {
                    blnTryDocumentAddition = false;
                }
               
                
            }

            return commitResult.Success;
        }

        private void ImportRun(object state)
        {
            int[] argumentArray = (int[])state;
            int startIndex = argumentArray[0];
            int fromDbBlock = argumentArray[1];
            HashSet<string> fileNameHash = new HashSet<string>();

            using (IContentServiceRepository ohvRepository = ohvRepositoryFactory.CreateInstance())
            {
                List<ImportItem> importItems = ohvRepository.GetImportItems(startIndex, fromDbBlock);
                List<IEnumerable<ImportItem>> importBlocks = CreateBlocks(importItems, OhvConfiguration.ItemsToProcess);

               
                    foreach (IEnumerable<ImportItem> importBlock in importBlocks)
                    {
                        List<DocumentBase> documentBlock = GenerateDocumentsFromImportItems(importBlock);

                        try
                        {
                            if (abortThreads)
                            {
                                break;
                            }



                            if (!TryDocumentAdditon(documentBlock, fileNameHash))
                            {
                                throw new Exception("An error occurred committing documents to SharePoint");
                            }

                            ohvRepository.MarkImported(importBlock, siteUrl);
                        }
                        catch (Exception ex)
                        {
                            var idsArray = (from imports in importBlock
                                            select imports.Id).ToArray();
                            string commaSeparatedIds = string.Join(",", idsArray);

                            Report("Attempting Rollback for document ids:" + commaSeparatedIds);
                            Report("Exception: " + ex.Message);
                            Rollback(documentBlock);
                            Report("Rollback successful");
                            abortThreads = true;
                        }
                    }
                
            }

            if (abortThreads)
            {
                Report(string.Format("Thread dealing with block {0} aborted", startIndex));
            }
            else
            {
                Report(string.Format("Thread processing block: {0} completed", startIndex));
            }

            semaphore.Release();
        }




        private void ImportDocuments(int numberToBuffer, int importsToProcess, int numberOfThreads, BackgroundWorker reportWork)
        {
            int count = Math.Min(ohvRepositoryFactory.CreateInstance().GetTotalToImportCount(), importsToProcess);
            semaphore = new Semaphore(numberOfThreads, numberOfThreads);

            for (int index = 0; index <= count; index += numberToBuffer)
            {
                semaphore.WaitOne();
                ThreadPool.QueueUserWorkItem(new WaitCallback(ImportRun), new int[] { index, GetMaxToBuffer(index, numberToBuffer, count) });
                reportWork.ReportProgress(index);

                if (abortThreads)
                {
                    break;
                }
            }
        }

        private int GetMaxToBuffer(int currentPosition, int numberToBuffer, int maxToBuffer)
        {
            int buffer = numberToBuffer;

            if (buffer + currentPosition > maxToBuffer)
            {
                buffer = maxToBuffer - currentPosition;
            }

            return buffer;
        }

        private void Report(string message)
        {
            Log.Instance.Info(message);
            Console.WriteLine(DateTime.Now + ": " + message);
        }

        public void Start()
        {
            abortThreads = false;
            Report("SharePoint Import Initialised");
            
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;

            Report("Processing: " + documentsToProcess);
            Report("Buffering: " + documentsToBuffer);

            worker.DoWork += new DoWorkEventHandler(
           delegate(object o, DoWorkEventArgs arguments)
           {
               BackgroundWorker reportWork = o as BackgroundWorker;
               ImportDocuments(documentsToBuffer, documentsToProcess, numberOfThreads, reportWork);
           });

            worker.ProgressChanged += new ProgressChangedEventHandler(
            delegate(object o, ProgressChangedEventArgs arguments)
            {
                Report(string.Format("Thread started processing block {0}", arguments.ProgressPercentage));
            });

            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(
            delegate(object o, RunWorkerCompletedEventArgs arguments)
            {
                Report("Finishing");

                for (int count = 0; count < numberOfThreads; count++)
                {
                    semaphore.WaitOne();
                }

                semaphore.Release(numberOfThreads);

                Report("Finished");
            });

            worker.RunWorkerAsync();
        }



        public void Stop()
        {
            this.abortThreads = true;
        }
    }
}
