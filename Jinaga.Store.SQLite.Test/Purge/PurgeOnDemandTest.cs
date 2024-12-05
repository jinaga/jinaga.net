using System.Text;
using Jinaga.DefaultImplementations;
using Jinaga.Store.SQLite.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jinaga.Store.SQLite.Test.Purge;

public class PurgeOnDemandTest
{
    private readonly SQLiteStore store;
    private readonly JinagaClient j;

    private static string SQLitePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "JinagaSQLiteTest",
        "PurgeOnDemandTest.db");

    public PurgeOnDemandTest()
    {
        if (File.Exists(SQLitePath))
            File.Delete(SQLitePath);

        PurgeConditions purgeConditions = PurgeConditions.Empty
            .Purge<Project>().WhenExists<ProjectDeleted>(deleted => deleted.project);
        store = new SQLiteStore(SQLitePath, NullLoggerFactory.Instance);
        j = new JinagaClient(store, new LocalNetwork(), purgeConditions.Validate(), NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task WhenPurgeEmptyDatabase_ThenNoEffect()
    {
        await j.Purge();

        var contents = await GetContents();
        contents.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenPurgeProjectWithNoSuccessors_ThenNoEffect()
    {
        var company = await j.Fact(new Company());
        var department = await j.Fact(new Department(company));
        var project = await j.Fact(new Project(department));
        var deleted = await j.Fact(new ProjectDeleted(project));

        await j.Purge();

        var contents = await GetContents();
        contents.Should().Be(
            """
            let f1: Company = {}

            let f2: Department = {
                company: f1
            }

            let f3: Project = {
                department: f2
            }

            let f4: Project.Deleted = {
                project: f3
            }


            """
        );
    }

    [Fact]
    public async Task WhenPurgeProjectWithSuccessors_ThenPurgesSuccessors()
    {
        var company = await j.Fact(new Company());
        var department = await j.Fact(new Department(company));
        var project = await j.Fact(new Project(department));
        var name = await j.Fact(new ProjectName(project, "Project", []));
        var modifiedName = await j.Fact(new ProjectName(project, "Modified Project", [name]));
        var deleted = await j.Fact(new ProjectDeleted(project));

        await j.Purge();

        var contents = await GetContents();
        contents.Should().Be(
            """
            let f1: Company = {}

            let f2: Department = {
                company: f1
            }

            let f3: Project = {
                department: f2
            }

            let f4: Project.Deleted = {
                project: f3
            }


            """
        );
    }

    private async Task<string> GetContents()
    {
        using var memoryStream = new MemoryStream();
        await j.ExportFactsToFactual(memoryStream);
        string contents = Encoding.UTF8.GetString(memoryStream.ToArray());
        return contents;
    }
}