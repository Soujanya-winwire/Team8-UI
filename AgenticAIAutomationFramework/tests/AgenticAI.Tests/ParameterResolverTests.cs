using AgenticAI.Core.DataDriven;
using NUnit.Framework;

namespace AgenticAI.Tests
{
    [TestFixture]
    public class ParameterResolverTests
    {
        [Test]
        public void InferParameterName_FromIdSelector_ReturnsCorrectName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterResolver.InferParameterName("#userEmail"), Is.EqualTo("email"));
                Assert.That(ParameterResolver.InferParameterName("#firstName"), Is.EqualTo("firstName"));
                Assert.That(ParameterResolver.InferParameterName("#lastName"), Is.EqualTo("lastName"));
                Assert.That(ParameterResolver.InferParameterName("#userPassword"), Is.EqualTo("password"));
                Assert.That(ParameterResolver.InferParameterName("#phoneNumber"), Is.EqualTo("phone"));
            });
        }

        [Test]
        public void InferParameterName_FromNameAttribute_ReturnsCorrectName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterResolver.InferParameterName("[name=email]"), Is.EqualTo("email"));
                Assert.That(ParameterResolver.InferParameterName("[name='user-email']"), Is.EqualTo("email"));
                Assert.That(ParameterResolver.InferParameterName("[name=\"password\"]"), Is.EqualTo("password"));
                Assert.That(ParameterResolver.InferParameterName("input[name=first_name]"), Is.EqualTo("firstName"));
            });
        }

        [Test]
        public void InferParameterName_FromTypeAttribute_ReturnsCorrectName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterResolver.InferParameterName("input[type=email]"), Is.EqualTo("email"));
                Assert.That(ParameterResolver.InferParameterName("input[type='password']"), Is.EqualTo("password"));
                Assert.That(ParameterResolver.InferParameterName("input[type=tel]"), Is.EqualTo("tel"));
            });
        }

        [Test]
        public void InferParameterName_FromDataTestId_ReturnsCorrectName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterResolver.InferParameterName("[data-testid=login-email]"), Is.EqualTo("loginEmail"));
                Assert.That(ParameterResolver.InferParameterName("[data-test='user-password']"), Is.EqualTo("password")); // Normalizes user-password → password
                Assert.That(ParameterResolver.InferParameterName("[data-qa=submit-button]"), Is.EqualTo("submitButton"));
            });
        }

        [Test]
        public void InferParameterName_ComplexSelector_ReturnsNull()
        {
            // Should return null for selectors we can't confidently parse
            var result = ParameterResolver.InferParameterName("div > span.class-name");
            Assert.That(result, Is.Null);
        }

        [Test]
        public void WrapAsPlaceholder_CreatesCorrectFormat()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterResolver.WrapAsPlaceholder("email"), Is.EqualTo("{{email}}"));
                Assert.That(ParameterResolver.WrapAsPlaceholder("username"), Is.EqualTo("{{username}}"));
                Assert.That(ParameterResolver.WrapAsPlaceholder("firstName"), Is.EqualTo("{{firstName}}"));
            });
        }

        [Test]
        public void ContainsPlaceholders_DetectsPlaceholders()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterResolver.ContainsPlaceholders("{{email}}"), Is.True);
                Assert.That(ParameterResolver.ContainsPlaceholders("${username}"), Is.True);
                Assert.That(ParameterResolver.ContainsPlaceholders("Hello {{name}}"), Is.True);
                Assert.That(ParameterResolver.ContainsPlaceholders("no placeholders here"), Is.False);
                Assert.That(ParameterResolver.ContainsPlaceholders("bob@test.com"), Is.False);
            });
        }

        [Test]
        public void ExtractPlaceholderNames_FindsAllPlaceholders()
        {
            var text = "Hello {{firstName}} {{lastName}}, your email is ${email}";
            var names = ParameterResolver.ExtractPlaceholderNames(text);

            Assert.Multiple(() =>
            {
                Assert.That(names.Count, Is.EqualTo(3));
                Assert.That(names, Does.Contain("firstName"));
                Assert.That(names, Does.Contain("lastName"));
                Assert.That(names, Does.Contain("email"));
            });
        }

        [Test]
        public void ResolveParameters_ReplacesPlaceholdersWithValues()
        {
            var dataset = new Dictionary<string, string>
            {
                { "firstName", "John" },
                { "lastName", "Doe" },
                { "email", "john.doe@test.com" }
            };

            Assert.Multiple(() =>
            {
                Assert.That(
                    ParameterResolver.ResolveParameters("{{email}}", dataset),
                    Is.EqualTo("john.doe@test.com")
                );

                Assert.That(
                    ParameterResolver.ResolveParameters("${firstName}", dataset),
                    Is.EqualTo("John")
                );

                Assert.That(
                    ParameterResolver.ResolveParameters("Hello {{firstName}} {{lastName}}", dataset),
                    Is.EqualTo("Hello John Doe")
                );
            });
        }

        [Test]
        public void AutoParameterize_CreatesPlaceholderFromLocator()
        {
            Assert.Multiple(() =>
            {
                // Should convert value to placeholder when locator is recognized
                Assert.That(
                    ParameterResolver.AutoParameterize("bob@test.com", "#userEmail", "Type", true),
                    Is.EqualTo("{{email}}")
                );

                Assert.That(
                    ParameterResolver.AutoParameterize("John", "#firstName", "Type", true),
                    Is.EqualTo("{{firstName}}")
                );

                Assert.That(
                    ParameterResolver.AutoParameterize("pass123", "[name=password]", "Type", true),
                    Is.EqualTo("{{password}}")
                );
            });
        }

        [Test]
        public void AutoParameterize_DisabledReturnsOriginalValue()
        {
            // When disabled, should return original value
            var result = ParameterResolver.AutoParameterize("bob@test.com", "#userEmail", "Type", false);
            Assert.That(result, Is.EqualTo("bob@test.com"));
        }

        [Test]
        public void AutoParameterize_AlreadyPlaceholderReturnsUnchanged()
        {
            // Should not double-wrap already parameterized values
            var result = ParameterResolver.AutoParameterize("{{email}}", "#userEmail", "Type", true);
            Assert.That(result, Is.EqualTo("{{email}}"));
        }

        [Test]
        public void AutoParameterize_UnrecognizedLocatorReturnsOriginalValue()
        {
            // Should return original value when we can't infer parameter name
            var result = ParameterResolver.AutoParameterize("Some Value", "div > span", "Type", true);
            Assert.That(result, Is.EqualTo("Some Value"));
        }

        [Test]
        public void InferParameterName_NormalizationWorks()
        {
            Assert.Multiple(() =>
            {
                // Test various naming conventions normalize properly
                Assert.That(ParameterResolver.InferParameterName("#user-name"), Is.EqualTo("name")); // Normalizes user-name → name
                Assert.That(ParameterResolver.InferParameterName("#user_email"), Is.EqualTo("email")); // Normalizes user_email → email
                Assert.That(ParameterResolver.InferParameterName("#FIRST_NAME"), Is.EqualTo("firstName")); // FIRST_NAME → firstName
                Assert.That(ParameterResolver.InferParameterName("#user_number"), Is.EqualTo("phone")); // user_number → phone (special case)
            });
        }

        [Test]
        public void InferParameterName_RemovesPrefixesSuffixes()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ParameterResolver.InferParameterName("#input-email"), Is.EqualTo("email"));
                Assert.That(ParameterResolver.InferParameterName("#field-password"), Is.EqualTo("password"));
                Assert.That(ParameterResolver.InferParameterName("#txt-username"), Is.EqualTo("username"));
                Assert.That(ParameterResolver.InferParameterName("#email-input"), Is.EqualTo("email"));
            });
        }

        [Test]
        public void InferParameterName_HandlesComplexIds()
        {
            Assert.Multiple(() =>
            {
                // For complex IDs, preserve meaningful structure (better for clarity and avoiding conflicts)
                Assert.That(ParameterResolver.InferParameterName("#registration-form-email-address"), Is.EqualTo("registrationFormEmailAddress"));
                Assert.That(ParameterResolver.InferParameterName("#checkout-billing-address"), Is.EqualTo("checkoutBillingAddress"));
                Assert.That(ParameterResolver.InferParameterName("#user-profile-phone-number"), Is.EqualTo("profilePhoneNumber")); // Normalizes user-profile-phone-number → profilePhoneNumber
            });
        }
    }
}
