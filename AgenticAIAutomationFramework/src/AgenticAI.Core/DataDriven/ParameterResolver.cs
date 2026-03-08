using System.Text.RegularExpressions;

namespace AgenticAI.Core.DataDriven
{
    /// <summary>
    /// Resolves placeholders in test data and infers parameter names from locators
    /// Supports data-driven testing with parameterized values
    /// </summary>
    public static class ParameterResolver
    {
        /// <summary>
        /// Resolve placeholders in a string using dataset values
        /// Supports both {{variable}} and ${variable} syntax
        /// </summary>
        /// <param name="value">String with placeholders like {{email}} or ${username}</param>
        /// <param name="dataset">Dictionary of parameter name → value</param>
        /// <returns>String with all placeholders replaced by actual values</returns>
        public static string ResolveParameters(string value, Dictionary<string, string> dataset)
        {
            if (string.IsNullOrEmpty(value) || dataset == null || dataset.Count == 0)
                return value;

            // Use existing SubstitutePlaceholders method
            return DataSetReader.SubstitutePlaceholders(value, dataset);
        }

        /// <summary>
        /// Infer a parameter name from a locator string
        /// Examples:
        ///   #userEmail → email
        ///   #firstName → firstName
        ///   [name=password] → password
        ///   input[type=email] → email
        ///   #user-name → userName
        /// </summary>
        /// <param name="locator">CSS selector or XPath</param>
        /// <param name="actionType">Type of action (Type, Select, etc.)</param>
        /// <returns>Inferred parameter name or null if unable to infer</returns>
        public static string? InferParameterName(string locator, string actionType = "Type")
        {
            if (string.IsNullOrWhiteSpace(locator))
                return null;

            // Strategy 1: Extract from common ID patterns
            // #userEmail → email, #firstName → firstName, #user-email → userEmail
            var idMatch = Regex.Match(locator, @"#([a-zA-Z][\w-]*)", RegexOptions.IgnoreCase);
            if (idMatch.Success)
            {
                return NormalizeParameterName(idMatch.Groups[1].Value);
            }

            // Strategy 2: Extract from name attribute
            // [name=user_email] → userEmail, [name='password'] → password
            var nameMatch = Regex.Match(locator, @"name\s*=\s*['""]?([a-zA-Z][\w-]*)['""]?", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                return NormalizeParameterName(nameMatch.Groups[1].Value);
            }

            // Strategy 3: Extract from data-testid or data-test attributes
            // [data-testid=login-email] → loginEmail
            var dataIdMatch = Regex.Match(locator, @"data-(?:test-?id|test|qa)\s*=\s*['""]?([a-zA-Z][\w-]*)['""]?", RegexOptions.IgnoreCase);
            if (dataIdMatch.Success)
            {
                return NormalizeParameterName(dataIdMatch.Groups[1].Value);
            }

            // Strategy 4: Extract from type attribute for input fields
            // input[type=email] → email, input[type=password] → password
            var typeMatch = Regex.Match(locator, @"type\s*=\s*['""]?([a-zA-Z]+)['""]?", RegexOptions.IgnoreCase);
            if (typeMatch.Success)
            {
                var type = typeMatch.Groups[1].Value.ToLower();
                if (IsCommonInputType(type))
                    return type; // email, password, tel, url
            }

            // Strategy 5: Extract from aria-label
            // [aria-label='User Email'] → userEmail
            var ariaMatch = Regex.Match(locator, @"aria-label\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            if (ariaMatch.Success)
            {
                return NormalizeParameterName(ariaMatch.Groups[1].Value);
            }

            // Strategy 6: Extract from placeholder attribute
            // [placeholder='Enter email'] → email
            var placeholderMatch = Regex.Match(locator, @"placeholder\s*=\s*['""]?([^'""]+)['""]?", RegexOptions.IgnoreCase);
            if (placeholderMatch.Success)
            {
                var placeholder = placeholderMatch.Groups[1].Value;
                // Extract meaningful word from placeholder
                var wordMatch = Regex.Match(placeholder, @"\b(email|password|username|name|phone|address|city|zip|postal)\b", RegexOptions.IgnoreCase);
                if (wordMatch.Success)
                    return wordMatch.Groups[1].Value.ToLower();
            }

            // Unable to infer a meaningful parameter name
            return null;
        }

        /// <summary>
        /// Wrap a parameter name in placeholder syntax {{paramName}}
        /// </summary>
        /// <param name="parameterName">Parameter name like 'email' or 'username'</param>
        /// <returns>Placeholder string like '{{email}}' or '{{username}}'</returns>
        public static string WrapAsPlaceholder(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return string.Empty;

            return $"{{{{{parameterName}}}}}";
        }

        /// <summary>
        /// Check if a string contains placeholders
        /// </summary>
        /// <param name="value">String to check</param>
        /// <returns>True if contains {{variable}} or ${variable} patterns</returns>
        public static bool ContainsPlaceholders(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return Regex.IsMatch(value, @"\{\{.+?\}\}|\$\{.+?\}");
        }

        /// <summary>
        /// Extract all placeholder names from a string
        /// </summary>
        /// <param name="value">String containing placeholders</param>
        /// <returns>List of parameter names (without braces)</returns>
        public static List<string> ExtractPlaceholderNames(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new List<string>();

            var names = new List<string>();

            // Extract {{variable}} patterns
            var doubleBraceMatches = Regex.Matches(value, @"\{\{([^}]+)\}\}");
            foreach (Match match in doubleBraceMatches)
            {
                names.Add(match.Groups[1].Value.Trim());
            }

            // Extract ${variable} patterns
            var dollarBraceMatches = Regex.Matches(value, @"\$\{([^}]+)\}");
            foreach (Match match in dollarBraceMatches)
            {
                names.Add(match.Groups[1].Value.Trim());
            }

            return names.Distinct().ToList();
        }

        /// <summary>
        /// Normalize a parameter name to camelCase format
        /// Examples:
        ///   user-email → userEmail
        ///   user_email → userEmail
        ///   USER_NAME → userName
        ///   First Name → firstName
        /// </summary>
        private static string NormalizeParameterName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            // Remove common prefixes/suffixes
            rawName = Regex.Replace(rawName, @"^(input-?|field-?|txt-?|user-?)", "", RegexOptions.IgnoreCase);
            rawName = Regex.Replace(rawName, @"(-?input|-?field|-?txt)$", "", RegexOptions.IgnoreCase);

            // Trim any remaining leading/trailing delimiters
            rawName = rawName.Trim('-', '_', ' ');

            // Split by delimiters: dash, underscore, space
            var words = Regex.Split(rawName, @"[-_\s]+").Where(w => !string.IsNullOrWhiteSpace(w)).ToArray();

            if (words.Length == 0)
                return rawName.ToLower();

            // Convert to camelCase: first word lowercase, rest capitalized
            var result = words[0].ToLower();
            for (int i = 1; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    result += char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
                }
            }

            // Handle special cases
            if (result == "useremail" || result == "emailaddress")
                return "email";
            if (result == "username" || result == "userid")
                return "username";
            if (result == "userpassword" || result == "pwd")
                return "password";
            if (result == "phonenumber" || result == "mobilenum" || result == "usernumber" || result == "number")
                return "phone";
            if (result == "firstname" || result == "fname")
                return "firstName";
            if (result == "lastname" || result == "lname" || result == "surname")
                return "lastName";
            if (result == "fullname")
                return "fullName";
            if (result == "streetaddress" || result == "addr")
                return "address";
            if (result == "zipcode" || result == "postalcode")
                return "zip";

            return result;
        }

        /// <summary>
        /// Check if a type is a common input type that can be used as parameter name
        /// </summary>
        private static bool IsCommonInputType(string type)
        {
            var commonTypes = new HashSet<string>
            {
                "email", "password", "tel", "tel", "phone", "url", "search", "number"
            };

            return commonTypes.Contains(type);
        }

        /// <summary>
        /// Auto-parameterize a value based on locator analysis
        /// If locator suggests a parameter name, wrap value as placeholder
        /// </summary>
        /// <param name="value">Original value typed by user</param>
        /// <param name="locator">Element locator</param>
        /// <param name="actionType">Action type (Type, Select, etc.)</param>
        /// <param name="enableAutoParameterization">Enable automatic parameterization</param>
        /// <returns>Either {{parameterName}} or original value</returns>
        public static string AutoParameterize(string value, string locator, string actionType = "Type", bool enableAutoParameterization = true)
        {
            if (!enableAutoParameterization || string.IsNullOrWhiteSpace(value))
                return value;

            // Check if value is already a placeholder
            if (ContainsPlaceholders(value))
                return value;

            // Try to infer parameter name from locator
            var paramName = InferParameterName(locator, actionType);

            if (!string.IsNullOrEmpty(paramName))
            {
                return WrapAsPlaceholder(paramName);
            }

            // Unable to infer - return original value
            return value;
        }

        /// <summary>
        /// Generate parameter mapping suggestions from a test scenario
        /// Analyzes all Type actions and suggests parameter names
        /// </summary>
        /// <param name="actions">List of recorded actions</param>
        /// <returns>Dictionary of locator → suggested parameter name</returns>
        public static Dictionary<string, string> GenerateParameterSuggestions(IEnumerable<object> actions)
        {
            var suggestions = new Dictionary<string, string>();

            // This would need to be implemented with proper action type checking
            // For now, return empty dictionary
            // TODO: Implement action analysis

            return suggestions;
        }
    }
}
