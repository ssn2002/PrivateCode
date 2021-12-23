

using System;
using System.Collections.Generic;
using ProjectFactory.BusinessObjects;
using ProjectFactory.NullableTypes;

namespace ProjectFactory.Telephony
{
    /// <summary>
    /// Immutable class to represent a UK telephone number
    /// </summary>
    [Serializable]
    public abstract class TelephoneNumber : ValidatedObject
    {
        #region Private Fields

        /// <summary>
        /// The value of the telephone number
        /// </summary>
        private readonly NullableString value;

        #endregion

        #region Protected Constructors

        /// <summary>
        /// Initializes a new instance of the TelephoneNumber class, requiring the telephone number as a string and the number of 
        /// digits that the string must contain
        /// </summary>
        /// <param name="value">The telephone number string</param>
        /// <param name="requiredNumberOfDigits">The number of digits the string must contain</param>
        /// <param name="maximumNumberOfSpaces">The maximum number of spaced allowed in the string</param>
        protected TelephoneNumber(NullableString value, int requiredNumberOfDigits, int maximumNumberOfSpaces)
        {
            string[] brokenRules = GetBrokenRules(value, requiredNumberOfDigits, maximumNumberOfSpaces);
            ChangeBrokenRules(brokenRules, true);

            this.value = CleanAndFormatValue(value);
        }

        /// <summary>
        /// Initializes a new instance of the TelephoneNumber class.
        /// </summary>
        protected TelephoneNumber() : this(new NullableString(), 1, 1)
        {
            // No implementation
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the telephone number
        /// </summary>
        public NullableString Value
        {
            get
            {
                return value;
            }
        }

        #endregion

        #region Public Static Methods
        
        /// <summary>
        /// Returns true if an input nullable string is a valid telephone number
        /// </summary>
        /// <param name="value">The telephone number string</param>
        /// <param name="requiredNumberOfDigits">The number of digits the string must contain</param>
        /// <param name="maximumNumberOfSpaces">The maximum number of spaced allowed in the string</param>
        /// <returns>True if the input is a valid telephone number</returns>
        public static bool IsValidTelephoneNumber(NullableString value, int requiredNumberOfDigits, int maximumNumberOfSpaces)
        {
            string[] brokenRules = GetBrokenRules(value, requiredNumberOfDigits, maximumNumberOfSpaces);
            return brokenRules.Length == 0;
        }

        #endregion

        #region Public Override Methods

        /// <summary>
        /// Override of the Equals method
        /// </summary>
        /// <param name="obj">The item to compare to this instance</param>
        /// <returns>True if obj is the same value as the current instance</returns>
        public override bool Equals(object obj)
        {
            TelephoneNumber telephoneNumber = obj as TelephoneNumber;

            if (telephoneNumber == null)
            {
                return false;
            }

            return telephoneNumber.value == this.value;
        }

        /// <summary>
        /// Gets the hash code for the instance
        /// </summary>
        /// <returns>The hash code</returns>
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        /// <summary>
        /// Returns the value of the telephone number
        /// </summary>
        /// <returns>The telephone number</returns>
        public override string ToString()
        {
            return value.ToString();
        }

        #endregion

        #region Private Static Methods

        /// <summary>
        /// Given a nullable string, all double spaces are removed where possible
        /// </summary>
        /// <param name="value">The input nullable string</param>
        /// <returns>A cleaned and formatted nullable string</returns>
        private static NullableString CleanAndFormatValue(NullableString value)
        {
            if (value == null || value.IsNull)
            {
                return value;
            }

            string stringValue = value.Value;
            stringValue = RemoveDoubleSpaces(stringValue);
            stringValue = stringValue.Trim();
            return new NullableString(stringValue);
        }

        /// <summary>
        /// Gets broken rules for a telephone number string
        /// </summary>
        /// <param name="value">The telephone number string</param>
        /// <param name="requiredNumberOfDigits">The number of digits the string must contain</param>
        /// <param name="maximumNumberOfSpaces">The maximum number of spaced allowed in the string</param>
        /// <returns>A list of broken rules</returns>
        private static string[] GetBrokenRules(NullableString value, int requiredNumberOfDigits, int maximumNumberOfSpaces)
        {
            List<string> brokenRulesList = new List<string>();

            value = CleanAndFormatValue(value);

            // Check for nulls:
            if (value == null || value.IsNull)
            {
                brokenRulesList.Add("Value is null");
            }

            int numberOfDigits = 0;
            int numberOfSpaces = 0;

            string stringValue = (value ?? new NullableString()).ToString();

            // Count up digits and spaces. 
            foreach (char c in stringValue)
            {
                if (char.IsNumber(c))
                {
                    numberOfDigits++;
                }

                if (c == ' ')
                {
                    numberOfSpaces++;
                }
            }

            // See if we have anything other than digits or spaces:
            if ((numberOfDigits + numberOfSpaces) != stringValue.Length)
            {
                brokenRulesList.Add("Value contains invalid characters");
            }

            // Check we have everything we need:
            if (numberOfDigits != requiredNumberOfDigits)
            {
                brokenRulesList.Add("Value contains incorrect number of digits");
            }

            if (numberOfSpaces > maximumNumberOfSpaces)
            {
                brokenRulesList.Add("Value contains incorrect number of spaces");
            }

            return brokenRulesList.ToArray();
        }

        /// <summary>
        /// Given a string, double spaces are replaced with single spaces
        /// </summary>
        /// <param name="value">The string to remove double spaces from</param>
        /// <returns>The string with double spaces removed</returns>
        private static string RemoveDoubleSpaces(string value)
        {
            while (value.Contains("  "))
            {
                value = value.Replace("  ", " ");
            }

            return value;
        }
    
        #endregion
    }
}
