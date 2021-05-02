using System.Collections.Generic;
using System.Xml;
using Xunit;

namespace Blend.Xml.Tests
{
    public class BasicTests
    {
        public class Contact
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }

            public Address Home { get; set; }
            public Address Work { get; set; }

            public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>();

            public string Notes { get; set; }
        }

        public class Address
        {
            public string Street { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Zip { get; set; }
        }

        [Fact]
        public void CanLexBasicXml()
        {
            // Create a collection to keep all the contact POCOs from the XML.
            var contacts = new List<Contact>();

            var lexer = new XmlLexer()
                // For every "contact" node, do the following...
                .OnElement("contact", contactNode =>
                {
                    // Create a Contact POCO to hold the parsed data.
                    var contact = new Contact();

                    // OnAttributes is called for every attribute in the node we're currently in.
                    // In this case, it's each attribute of the `contact` node.
                    // You can also target specific attributes with `OnAttributeValue`
                    contactNode.OnAttributes((k, v) => contact.Metadata[k] = v);

                    // Descend into the `name` node.
                    contactNode.OnElement("name", nameNode =>
                    {
                        // OnElementText pulls text embedded within a node.
                        // For example, this would pull "Something" form `<first>Something</first>`
                        nameNode.OnElementText("first", first => contact.FirstName = first);
                        nameNode.OnElementText("last", last => contact.LastName = last);
                    });
                    contactNode.OnElementText("email", email => contact.Email = email);
                    contactNode.OnElement("address", addressNode =>
                    {
                        // Contrived example here. In the XML, the work/home address is differentiated
                        // by an attribute value. So we default to "home" address, then build the address,
                        // then use the `type` attribute value to differentiate.
                        
                        // Value to hold the type attribute value.
                        string type = "home"; // default

                        // Create a new Address POCO
                        Address address = new Address();

                        // Gather the values
                        addressNode.OnAttributeValue("type", x => type = x);
                        addressNode.OnElementText("street", x => address.Street = x);
                        addressNode.OnElementText("city", x => address.City = x);
                        addressNode.OnElementText("state", x => address.State = x);
                        addressNode.OnElementText("zip", x => address.Zip = x);

                        // `OnElementClose` fires when the "end" of the element is reached.
                        // Useful for collecting the results generated above.
                        addressNode.OnElementClose(() =>
                        {
                            // Now that entire element has been processed, we can set the correct
                            // value in the POCO.
                            switch (type)
                            {
                                case "work":
                                    contact.Work = address;
                                    break;
                                default:
                                    contact.Home = address;
                                    break;
                            }
                        });
                    });

                    // `OnElementText` will also grab CDATA nodes
                    contactNode.OnElementText("notes", notes => contact.Notes = notes);

                    // Use `OnElementClose` to gather the results of the node.
                    // In this case, adding `contact` to the list of `contacts`.
                    contactNode.OnElementClose(() => contacts.Add(contact));
                });

            using (var stream = GetType().Assembly.GetManifestResourceStream("Blend.Xml.Tests.test.xml"))
            {
                // Once you've built your lexer, you can execute it against a `XmlTextReader`
                lexer.Execute(new XmlTextReader(stream));
            }

            Assert.Single(contacts);
            var result = contacts[0];

            Assert.Equal(2, result.Metadata.Count);
            Assert.Equal("6db061b4-e11a-4e7e-927a-7c8a3b8d31bc", result.Metadata["id"]);
            Assert.Equal("0a735c79-49cc-4bd7-acf4-b85cd221ad90", result.Metadata["externalid"]);
            Assert.Equal("Steve", result.FirstName);
            Assert.Equal("Stevenson", result.LastName);
            Assert.Equal("test@example.com <Steve Stevenson>", result.Email);
            Assert.Equal("123 Test Ave", result.Home.Street);
            Assert.Equal("Testington", result.Home.City);
            Assert.Equal("TS", result.Home.State);
            Assert.Equal("00000", result.Home.Zip);
            Assert.Equal("321 Example St", result.Work.Street);
            Assert.Equal("Exampleville", result.Work.City);
            Assert.Equal("EX", result.Work.State);
            Assert.Equal("55555", result.Work.Zip);
            Assert.Equal("<p>Notes about Steve.</p>", result.Notes);
        }
    }
}
