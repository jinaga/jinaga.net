using System;
using System.Collections.Generic;

namespace Jinaga.Test;

[FactType("CoLab.Project")]
record ColabProject(User creator, DateTime createdAt) {}

[FactType("CoLab.Project.Name")]
record ColabProjectName(ColabProject project, string value, ColabProjectName[] prior) {}

public class LoadTest
{
    [Fact]
    public static async Task Load()
    {
        var j = JinagaClient.Create(opt =>
        {
            opt.HttpEndpoint = new Uri("http://localhost:8080/jinaga");
        });

        var michael = await j.Fact(new User("---PUBLIC KEY---Michael---END PUBLIC KEY---"));
        var wingman = await j.Fact(new ColabProject(michael, DateTime.Parse("2024-04-07T15:00:00.000Z")));
        ColabProjectName prior = await j.Fact(new ColabProjectName(wingman, $"Initial name", new ColabProjectName[0]));
        List<ColabProjectName> concurrentNames = new List<ColabProjectName>();
        for (var i = 0; i < 1000; i++)
        {
            var name = new ColabProjectName(wingman, $"New name {i}", new[] { prior });
            concurrentNames.Add(name);
        }

        var resolution = await j.Fact(new ColabProjectName(wingman, $"Final name", concurrentNames.ToArray()));
    }
}