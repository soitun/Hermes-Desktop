using System.Net;
using Hermes.Agent.Skills;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Skills;

[TestClass]
public sealed class SkillsHubTests
{
    private string _skillsDir = "";
    private string _quarantineDir = "";

    [TestInitialize]
    public void SetUp()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hermes-skills-hub-tests-{Guid.NewGuid():N}");
        _skillsDir = Path.Combine(root, "skills");
        _quarantineDir = Path.Combine(root, "quarantine");
        Directory.CreateDirectory(_skillsDir);
        Directory.CreateDirectory(_quarantineDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        var root = Path.GetDirectoryName(_skillsDir);
        if (root is not null && Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task InstallAsync_PreservesDownloadedSkillFrontmatter()
    {
        const string skillContent = """
            ---
            name: upstream-skill
            description: Remote description
            tools: read_file, write_file
            model: qwen-plus
            ---
            Use the remote instructions.
            """;
        var handler = new StubHttpMessageHandler(_ => TextResponse(skillContent));
        var manager = NewManager();
        var hub = NewHub(manager, handler);

        var result = await hub.InstallAsync("remote-skill", "https://example.test/SKILL.md", CancellationToken.None);

        Assert.IsTrue(result.Success, result.Error);
        var installed = manager.GetSkill("remote-skill");
        Assert.IsNotNull(installed);
        Assert.AreEqual("Remote description", installed.Description);
        CollectionAssert.AreEqual(new[] { "read_file", "write_file" }, installed.Tools);
        Assert.AreEqual("qwen-plus", installed.Model);
        StringAssert.Contains(installed.SystemPrompt, "remote instructions");
    }

    [TestMethod]
    public async Task SearchGitHubAsync_RecursesOneCategoryLevel()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            if (path.EndsWith("/repos/owner/repo/contents/skills", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    [
                      { "name": "root-skill.md", "type": "file", "download_url": "https://raw.test/root.md" },
                      { "name": "code", "type": "dir", "download_url": null }
                    ]
                    """);
            }

            if (path.EndsWith("/repos/owner/repo/contents/skills/code", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    [
                      { "name": "nested-skill.md", "type": "file", "download_url": "https://raw.test/nested.md" }
                    ]
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var hub = NewHub(NewManager(), handler);

        var results = await hub.SearchGitHubAsync("https://github.com/owner/repo", CancellationToken.None);

        CollectionAssert.Contains(results.Select(r => r.Name).ToList(), "root-skill");
        CollectionAssert.Contains(results.Select(r => r.Name).ToList(), "nested-skill");
        Assert.IsTrue(handler.Requests.Any(uri => uri.AbsolutePath.EndsWith("/skills/code", StringComparison.Ordinal)));
    }

    private SkillManager NewManager() =>
        new(_skillsDir, NullLogger<SkillManager>.Instance);

    private SkillsHub NewHub(SkillManager manager, HttpMessageHandler handler) =>
        new(manager, _quarantineDir, NullLogger<SkillsHub>.Instance, new HttpClient(handler));

    private static HttpResponseMessage TextResponse(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text) };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not null)
                Requests.Add(request.RequestUri);

            return Task.FromResult(_responder(request));
        }
    }
}
