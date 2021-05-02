# Blend.XmlLexer

`Blend.XmlLexer` is a small utility for quickly parsing large XML files. It works by wrapping `XmlTextReader` with some a builder-like interface to create (relatively) simple parsing pipelines for forward-only XML parsing.

This utility is primarily useful in situations where you have very large XML files that you don't want to hold him memory as a fully parsed DOM, but need to extra some information from the XML. For example, you might be given an XML dump of database, but you want to extract some contact information from it.

## Usage

Below is an admittedly convoluted example of converting XML to a simple C# POCO that doesn't quite match the XML structure.

```xml
    <root>
    <contact id="6db061b4-e11a-4e7e-927a-7c8a3b8d31bc" externalid="0a735c79-49cc-4bd7-acf4-b85cd221ad90">
        <name>
        <first>Steve</first>
        <last>Stevenson</last>
        </name>
        <email>test@example.com &lt;Steve Stevenson&gt;</email>
        <address type="home">
        <street>123 Test Ave</street>
        <city>Testington</city>
        <state>TS</state>
        <zip>00000</zip>
        </address>
        <address type="work">
        <street>321 Example St</street>
        <city>Exampleville</city>
        <state>EX</state>
        <zip>55555</zip>
        </address>
        <notes><![CDATA[<p>Notes about Steve.</p>]]></notes>
    </contact>
    <site>
        <url>https://www.example.com/</url>
        <notes>This is only here to ensure non-matching nodes do not get processed.</notes>
    </site>
    </root>
```

Then you would use the `XmlLexer` class to build, and then execute, a basic XML to POCO parser:

```csharp
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

    using (var stream = File.OpenRead("test.xml")))
    {
        // Once you've built your lexer, you can execute it against a `XmlTextReader`
        lexer.Execute(new XmlTextReader(stream));
    }
```

## Caveats

This is a forward-only parser. You can't do things that require a full DOM like look up parent nodes, previous sibling, etc.
