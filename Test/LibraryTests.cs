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
    [Fact]
    public void Test()
    {

    }

    [InlineData("single", """
        {
            single: [
                {
                    title: "Title",
                    author: "Author",
                    primary_square_chan: 0,
                    uses: ["1", "2"],
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
    public void LoadLibraryImmediateSucceeds(
        string _, // Test name
        string jsonData,
        int numSingle,
        int numGroups)
    {
        var lib = LoadLibrary(jsonData);

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
    public void LoadLibraryImmediateFails(
        string _, // Test name
        string jsonData,
        object? errRegex = null,
        object? atRegex = null,
        RegexOptions? regexOpts = null,
        RegexOptions? atRegexOpts = null)
    {
        var ex = Assert.Throws<ParsingError>( () => LoadLibrary(jsonData));
     
        AssertRegexMatches(
            $"{ex.Message}\n{ex.Submessage ?? string.Empty}", 
            errRegex, 
            regexOpts);

        AssertRegexMatches(ex.AtString, atRegex, atRegexOpts);
    }

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


    private FtLibraryInfo LoadLibrary(string jsonData)
    {
        return (FtLibraryInfo)FtLibraryInfo.Parse<FtLibraryInfo>(jsonData);
    }

    private FtLibraryInfo LoadLibraryFile(string path)
    {
        string jsonData = File.ReadAllText(path);
        
        return LoadLibrary(jsonData);
    }
}