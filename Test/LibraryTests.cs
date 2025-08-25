using Newtonsoft.Json;
using System.IO;
using Xunit;

//using FtRandoLib.Importer;
using FtRandoLib.Library;
using System.Text.RegularExpressions;

// This needs a LOT more test cases

namespace Test;

public class LibraryTests
{
    [InlineData("hex",
        "hex:10000f000f000f000f0000100eb80b0012001a000140069600001C0026002A002A002A002A008800003F003F",
        "10000f000f000f000f0000100eb80b0012001a000140069600001c0026002a002a002a002a008800003f003f")]
    [InlineData("base64", 
        "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=", 
        "10000f000f000f000f0000100eb80b0012001a000140069600001c0026002a002a002a002a008800003f003f")]
    [InlineData("deflate",
        "deflate:E2Dgh0IGAb4d3AxCDFIMjIyM0xgYZBjUoFCDgYGhA4gB",
        "10000F000F000F000F0000100EB80B0012001A000101019600001C0026002600260026002800000088000000")]
    [Theory]
    public void DataDecompressSucceeds(
        string _, // Test name
        string dataStr,
        string hexData)
    {
        FtModuleInfo info = new() { Data = dataStr };

        Assert.Equal(info.UncompressedData, Convert.FromHexString(hexData));
    }

    [InlineData("single", """
        {
            single: [
                {
                    title: "Title",
                    author: "Author",
                    primary_square_chan: 0,
                    uses: ["1", "2"],
                    start_addr: 1000,
                    data: "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=",
                },
            ],
        }
        """, 1, 0)]
    [InlineData("groups", """
        {
            groups: [
                {
                    title: "Group",
                    author: "Author",
                    primary_square_chan: 0,
                    items: [
                        {
                            title: "Title1",
                            author: "Author1",
                            primary_square_chan: 1,
                            uses: ["1", "2"],
                            start_addr: "1000",
                            data: "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=",
                        },
                        {
                            title: "Title2",
                            data: "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=",
                        },
                        {
                            title: "Title3",
                            data: "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=",
                        },
                    ],
                },
            ],
        }
        """, 0, 1)]
    [Theory]
    public void LoadJsonLibraryImmediateSucceeds(
        string _, // Test name
        string jsonData,
        int numSingle,
        int numGroups)
    {
        var lib = LoadJsonLibrary(jsonData);

        Assert.Equal(lib.Single.Count, numSingle);
        Assert.Equal(lib.Groups.Count, numGroups);
    }

    [InlineData(
        "missing '}'", 
        "{", 
        "unexpected end",
        new string[] { "line 1", "position 1" })]
    [InlineData(
        "extra '}'", 
        "{}}", 
        "Additional text",
        "line 1")] // Position is ambiguous
    [InlineData(
        "missing ']'", 
        "{single: [}", 
        "Unexpected character",
        new string[] { "line 1, position 10" })]
    [InlineData(
        "extra ']'", 
        "{single: []]}", 
        "EndArray is not valid",
        "line 1")] // Position is ambiguous
    [InlineData(
        "invalid base64", 
        """
            {
                single: [
                    {
                        title: "Title",
                        author: "Author",
                        primary_square_chan: 0,
                        uses: ["1", "2"],
                        "data": "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8",
                    },
                ],
            }
        """, 
        new string[] { "Error setting value to 'Data'", "not a valid Base-64 string" },
        "with title 'Title'")]
    [InlineData(
        "invalid deflate",
        """
            {
                single: [
                    {
                        title: "Title",
                        author: "Author",
                        primary_square_chan: 0,
                        uses: ["1", "2"],
                        "data": "deflate:EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=",
                    },
                ],
            }
        """,
        new string[] { "Error setting value to 'Data'", "unsupported compression method" },
        "with title 'Title'")]
    [InlineData(
        "missing field",
        """
            {
                single: [
                    {
                        title: "Title",
                        author: "Author",
                        primary_square_chan: 0,
                        uses: ["1", "2"],
                    },
                ],
            }
        """,
        "'data' not found",
        new string[] { "line 8", "position 13", "with title 'Title'" })]
    [InlineData(
        "extra field",
        """
            {
                single: [
                    {
                        title: "Title",
                        author: "Author",
                        primary_square_chan: 0,
                        uses: ["1", "2"],
                        "data": "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=",
                        goat: true,
                    },
                ],
            }
        """,
        "could not find member 'goat'",
        new string[] { "line 9", "position 21", "with title 'Title'" })]
    [Theory]
    public void LoadJsonLibraryImmediateFails(
        string _, // Test name
        string jsonData,
        object? errRegex = null,
        object? atRegex = null,
        RegexOptions? regexOpts = null,
        RegexOptions? atRegexOpts = null)
    {
        var ex = Assert.Throws<ParsingError>(() => LoadJsonLibrary(jsonData));
     
        AssertRegexMatches(
            $"{ex.Message}\n{ex.Submessage ?? string.Empty}", 
            errRegex, 
            regexOpts);

        AssertRegexMatches(ex.AtString, atRegex, atRegexOpts);
    }

    [InlineData("single", """
        single:
            -   title: Title
                author: 'Author'
                primary_square_chan: 0
                uses: [1, "2"]
                start_addr: 1000
                data: EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8=
        """, 1, 0)]
    [InlineData("groups", """
        groups:
            -   title: "Group"
                author: "Author"
                primary_square_chan: "0"
                items:
                    -   title: "Title1"
                        author: "Author1"
                        primary_square_chan: 1
                        uses: 
                            - 1
                            - "2"
                        start_addr: 0x1000
                        data: "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8="
                    -   title: "Title2"
                        start_addr: $1000
                        data: "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8="
                    -   title: "Title3"
                        start_addr: "1000"
                        start_addr: "0x1000"
                        start_addr: "$1000"
                        data: hex:10000f000f000f000f0000100eb80b0012001a000140069600001c0026002a002a002a002a008800003f003f
        """, 0, 1)]
    [Theory]
    public void LoadYamlLibraryImmediateSucceeds(
        string _, // Test name
        string yamlData,
        int numSingle,
        int numGroups)
    {
        var lib = LoadYamlLibrary(yamlData);

        Assert.Equal(lib.Single.Count, numSingle);
        Assert.Equal(lib.Groups.Count, numGroups);
    }

    [InlineData(
        "string assigned to int",
        """
        single:
            -   title: "Title"
                author: "Author"
                primary_square_chan: goat
                uses: ["1", "2"]
                "data": "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8="
        """,
        "not in a correct format",
        new string[] { "line 4", "position 30", "with title 'Title'" })]
    [InlineData(
        "invalid base64",
        """
        single:
            -   title: "Title"
                author: "Author"
                primary_square_chan: 0
                uses: ["1", "2"]
                "data": "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8"
        """,
        new string[] { /*"Error setting value to 'Data'",*/ "not a valid Base-64 string" },
        new string[] { "line 6", "position 9", "with title 'Title'" })]
    [InlineData(
        "invalid deflate",
        """
        single:
            -   title: "Title"
                author: "Author"
                primary_square_chan: 0
                uses: ["1", "2"]
                "data": "deflate:EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8="
        """,
        new string[] { /*"Error setting value to 'Data'",*/ "unsupported compression method" },
        new string[] { "line 6", "position 9", "with title 'Title'" })]
    [InlineData(
        "missing field",
        """
        single:
            -   title: "Title"
                author: "Author"
                primary_square_chan: 0
                uses: ["1", "2"]
        """,
        "The Data field is required.",
        new string[] { "line 2", "position 9", "with title 'Title'" })]
    [InlineData(
        "extra field",
        """
        single:
            -   title: "Title"
                author: "Author"
                primary_square_chan: 0
                uses: ["1", "2"]
                "data": "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8="
                goat: true
        """,
        "Property 'goat' not found",
        new string[] { "line 7", "position 9", "with title 'Title'" })]
    [Theory]
    public void LoadYamlLibraryImmediateFails(
        string _, // Test name
        string yamlData,
        object? errRegex = null,
        object? atRegex = null,
        RegexOptions? regexOpts = null,
        RegexOptions? atRegexOpts = null)
    {
        var ex = Assert.Throws<ParsingError>(() => LoadYamlLibrary(yamlData));

        AssertRegexMatches(
            $"{ex.Message}\n{ex.Submessage ?? string.Empty}",
            errRegex,
            regexOpts);

        AssertRegexMatches(ex.AtString, atRegex, atRegexOpts);
    }

    /*[InlineData(
        "tags work",
        """
            {
                single: [
                    {
                        title: "Title",
                        //author: "Author",
                        "data": "EAAPAA8ADwAPAAAQDrgLABIAGgABQAaWAAAcACYAKgAqACoAKgCIAAA/AD8",
                    },
                ],
            }
        """,
        )]
    [Theory]
    public void LoadLibraryTagTest(
        string _, // Test name
        string jsonData,
        IstringSet tags)
    {
        var ex = LoadLibrary(jsonData);

        if (ex.Single.Count != 0)
        {
            Assert.Single(ex.Single);
            Assert.Empty(ex.Groups);
            Assert.True(ex.Single[0].Tags.SetEquals(tags));
        }
        else
        {
            Assert.Empty(ex.Single);
            Assert.Single(ex.Groups);
            Assert.Single(ex.Groups[0].Items);
            Assert.True(ex.Groups[0].Items[0].Tags.SetEquals(tags));
        }
    }*/

    /*[Theory]
    public void LoadLibrarySucceeds(
        string path,
        int numSingle,
        int numGroups)
    {
        try
        {
            var lib = LoadLibrary(path);

            Assert.Equal(lib.Single.Count, numSingle);
            Assert.Equal(lib.Groups.Count, numGroups);
        }
        catch (Exception e)
        {
            throw;
        }
    }*/

    private static void AssertRegexMatches(
        string? str,
        object? regex, 
        RegexOptions? regexOpts)
    {
        if (regex is null)
            return;

        if (str is null)
            str = "";

        var defRegexOpts = RegexOptions.IgnoreCase;
        if (regex is string regexStr)
            Assert.Matches(new Regex(regexStr, regexOpts ?? defRegexOpts), str);
        else if (regex is IEnumerable<string> regexList)
        {
            foreach (var listStr in regexList)
                Assert.Matches(new Regex(listStr, regexOpts ?? defRegexOpts), str);
        }
    }


    private FtLibraryInfo LoadJsonLibrary(string jsonData)
    {
        return (FtLibraryInfo)FtLibraryInfo.ParseJson<FtLibraryInfo>(jsonData);
    }

    private FtLibraryInfo LoadJsonLibraryFile(string path)
    {
        string jsonData = File.ReadAllText(path);
        
        return LoadJsonLibrary(jsonData);
    }

    private FtLibraryInfo LoadYamlLibrary(string jsonData)
    {
        return (FtLibraryInfo)FtLibraryInfo.ParseYaml<FtLibraryInfo>(jsonData);
    }

    private FtLibraryInfo LoadYamlLibraryFile(string path)
    {
        string yamlData = File.ReadAllText(path);

        return LoadYamlLibrary(yamlData);
    }
}