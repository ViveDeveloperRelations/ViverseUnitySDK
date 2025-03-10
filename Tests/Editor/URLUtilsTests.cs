using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ViverseWebGLAPI;
using System.Linq;

[TestFixture]
public class URLUtilsTests
{
    

    [Test]
    public void URLParts_CanReconstructURL_Simple()
    {
        // Arrange
        string originalUrl = "https://example.com/path";
        
        // Act
        var parts = URLUtils.ParseURL(originalUrl);
        string reconstructedUrl = parts.ReconstructURL();
        
        // Assert
        Assert.That(reconstructedUrl, Is.EqualTo(originalUrl));
    }

    [Test]
    public void URLParts_CanReconstructURL_Complex()
    {
        // Arrange
        string originalUrl = "https://example.com:8080/path/to/resource?name=John%20Doe&age=25#section";
        
        // Act
        var parts = URLUtils.ParseURL(originalUrl);
        string reconstructedUrl = parts.ReconstructURL();
        
        // Parse both URLs again to normalize them for comparison
        var originalParts = URLUtils.ParseURL(originalUrl);
        var reconstructedParts = URLUtils.ParseURL(reconstructedUrl);
        
        // Assert
        Assert.That(reconstructedParts.Protocol, Is.EqualTo(originalParts.Protocol));
        Assert.That(reconstructedParts.Hostname, Is.EqualTo(originalParts.Hostname));
        Assert.That(reconstructedParts.Port, Is.EqualTo(originalParts.Port));
        Assert.That(reconstructedParts.Pathname, Is.EqualTo(originalParts.Pathname));
        Assert.That(reconstructedParts.Parameters, Is.EquivalentTo(originalParts.Parameters));
        Assert.That(reconstructedParts.Fragment, Is.EqualTo(originalParts.Fragment));
    }

    [Test]
    public void URLParts_CanReconstructURL_WithSpecialCharacters()
    {
        // Arrange
        string originalUrl = "https://example.com/path?name=John%20Doe&email=john%40example.com#top";
        
        // Act
        var parts = URLUtils.ParseURL(originalUrl);
        string reconstructedUrl = parts.ReconstructURL();
        var reconstructedParts = URLUtils.ParseURL(reconstructedUrl);
        
        // Assert
        Assert.That(reconstructedParts.Parameters["name"], Is.EqualTo("John Doe"));
        Assert.That(reconstructedParts.Parameters["email"], Is.EqualTo("john@example.com"));
        Assert.That(reconstructedParts.Fragment, Is.EqualTo("top"));
    }
        


    [Test]
    public void ParseURL_BasicURL_ReturnsCorrectParts()
    {
        // Arrange
        string url = "https://example.com/path";

        // Act
        var result = URLUtils.ParseURL(url);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Protocol, Is.EqualTo("https"));
        Assert.That(result.Hostname, Is.EqualTo("example.com"));
        Assert.That(result.Pathname, Is.EqualTo("/path"));
        Assert.That(result.Port, Is.Empty);
        Assert.That(result.Search, Is.Empty);
        Assert.That(result.Fragment, Is.Empty);
        Assert.That(result.Parameters, Is.Empty);
    }

    [Test]
    public void ParseURL_ComplexURL_ReturnsCorrectParts()
    {
        // Arrange
        string url = "https://example.com:8080/path/to/page?param1=value1&param2=value2#section";

        // Act
        var result = URLUtils.ParseURL(url);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Protocol, Is.EqualTo("https"));
        Assert.That(result.Hostname, Is.EqualTo("example.com"));
        Assert.That(result.Port, Is.EqualTo("8080"));
        Assert.That(result.Pathname, Is.EqualTo("/path/to/page"));
        Assert.That(result.Search, Is.EqualTo("?param1=value1&param2=value2"));
        Assert.That(result.Fragment, Is.EqualTo("section"));
        
        // Check parameters
        Assert.That(result.Parameters.Count, Is.EqualTo(2));
        Assert.That(result.Parameters["param1"], Is.EqualTo("value1"));
        Assert.That(result.Parameters["param2"], Is.EqualTo("value2"));
    }

    [Test]
    public void ParseURL_URLWithEncodedParameters_DecodesCorrectly()
    {
        // Arrange
        string url = "https://example.com/path?name=John%20Doe&email=john%40example.com";

        // Act
        var result = URLUtils.ParseURL(url);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Parameters["name"], Is.EqualTo("John Doe"));
        Assert.That(result.Parameters["email"], Is.EqualTo("john@example.com"));
    }

    [Test]
    public void ParseURL_URLWithDuplicateParameters_UsesFirstValue()
    {
        // Expect the warning log about duplicate parameters
        LogAssert.Expect(LogType.Error, "URL parameter 'param' has multiple values: value1, value2. Using first value: value1");

        // Arrange
        string url = "https://example.com/path?param=value1&param=value2";

        // Act
        var result = URLUtils.ParseURL(url);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Parameters.Count, Is.EqualTo(1));
        Assert.That(result.Parameters["param"], Is.EqualTo("value1"));
    }

    [Test]
    public void ParseURL_InvalidURL_ReturnsNull()
    {
        // Expect the error log about invalid URL
        LogAssert.Expect(LogType.Error, "Failed to parse URL: Invalid URI: The format of the URI could not be determined.");
        
        // Arrange
        string url = "not a valid url";

        // Act
        var result = URLUtils.ParseURL(url);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseURL_StandardPorts_OmitsPort()
    {
        // Arrange
        string httpUrl = "http://example.com:80/path";
        string httpsUrl = "https://example.com:443/path";

        // Act
        var httpResult = URLUtils.ParseURL(httpUrl);
        var httpsResult = URLUtils.ParseURL(httpsUrl);

        // Assert
        Assert.That(httpResult.Port, Is.Empty);
        Assert.That(httpsResult.Port, Is.Empty);
    }

    [Test]
    public void GetURLParts_ValidURL_ReturnsTuple()
    {
        // Arrange
        string url = "https://example.com:8080/path?param=value#fragment";

        // Act
        var (protocol, hostname, pathname, fragment, parameters) = URLUtils.GetURLParts(url);

        // Assert
        Assert.That(protocol, Is.EqualTo("https"));
        Assert.That(hostname, Is.EqualTo("example.com"));
        Assert.That(pathname, Is.EqualTo("/path"));
        Assert.That(fragment, Is.EqualTo("fragment"));
        Assert.That(parameters.Count, Is.EqualTo(1));
        Assert.That(parameters["param"], Is.EqualTo("value"));
    }

    [Test]
    public void GetURLParts_InvalidURL_ReturnsDefaultTuple()
    {
// Expect the error log about invalid URL
        LogAssert.Expect(LogType.Error, "Failed to parse URL: Invalid URI: The format of the URI could not be determined.");        
        // Arrange
        string url = "invalid url";

        // Act
        var (protocol, hostname, pathname, fragment, parameters) = URLUtils.GetURLParts(url);

        // Assert
        Assert.That(protocol, Is.Null);
        Assert.That(hostname, Is.Null);
        Assert.That(pathname, Is.Null);
        Assert.That(fragment, Is.Null);
        Assert.That(parameters, Is.Null);
    }

    [Test]
    public void URLParts_GetFullHostname_WithPort_ReturnsHostAndPort()
    {
        // Arrange
        var urlParts = new URLUtils.URLParts
        {
            Hostname = "example.com",
            Port = "8080"
        };

        // Act
        string result = urlParts.GetFullHostname();

        // Assert
        Assert.That(result, Is.EqualTo("example.com:8080"));
    }

    [Test]
    public void URLParts_GetFullHostname_WithoutPort_ReturnsOnlyHost()
    {
        // Arrange
        var urlParts = new URLUtils.URLParts
        {
            Hostname = "example.com",
            Port = ""
        };

        // Act
        string result = urlParts.GetFullHostname();

        // Assert
        Assert.That(result, Is.EqualTo("example.com"));
    }

    [Test]
    public void URLParts_ToString_ReturnsFormattedString()
    {
        // Arrange
        var urlParts = new URLUtils.URLParts
        {
            Protocol = "https",
            Hostname = "example.com",
            Pathname = "/path",
            Search = "?param=value",
            Fragment = "section",
            Parameters = new Dictionary<string, string> { { "param", "value" } }
        };

        // Act
        string result = urlParts.ToString();

        // Assert
        Assert.That(result, Does.Contain("Protocol: https"));
        Assert.That(result, Does.Contain("Hostname: example.com"));
        Assert.That(result, Does.Contain("Pathname: /path"));
        Assert.That(result, Does.Contain("Search: ?param=value"));
        Assert.That(result, Does.Contain("Fragment: section"));
        Assert.That(result, Does.Contain("Parameters: param=value"));
    }
}