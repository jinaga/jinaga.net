﻿using FluentAssertions;
using Jinaga.Storage;
using Jinaga.Test.Fakes;
using Jinaga.Test.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Jinaga.Test.Observers;
public class WatchFromNetworkTest
{
    private ITestOutputHelper output;

    public WatchFromNetworkTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Watch_EmptyUpstream()
    {
        var j = new JinagaClient(new MemoryStore(), new FakeNetwork(output));

        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().BeEmpty();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_SingleUnnamedOffice()
    {
        var network = new FakeNetwork(output);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        });

        var j = new JinagaClient(new MemoryStore(), network);

        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().ContainSingle().Which
                .Name.Should().BeNull();
        }
        finally
        {
            watch.Stop();
        }
    }

    [Fact]
    public async Task Watch_SingleNamedOffice()
    {
        var network = new FakeNetwork(output);
        var contoso = new Company("contoso");
        var dallas = new City("Dallas");
        var dallasOffice = new Office(contoso, dallas);
        var dallasOfficeName = new OfficeName(dallasOffice, "Dallas", new OfficeName[0]);
        network.AddFeed("offices", new object[]
        {
            dallasOffice
        }, 1);
        network.AddFeed("officeNames", new object[]
        {
            dallasOfficeName
        }, 1);

        var j = new JinagaClient(new MemoryStore(), network);

        var viewModel = new CompanyViewModel();
        var watch = viewModel.Load(j, "contoso");

        try
        {
            await watch.Loaded;
            viewModel.Offices.Should().ContainSingle().Which
                .Name.Should().Be("Dallas");
        }
        finally
        {
            watch.Stop();
        }
    }

    private class OfficeViewModel
    {
        public Office Office;
        public string Name;
    }

    private class CompanyViewModel
    {
        public List<OfficeViewModel> Offices = new();

        public IObserver Load(JinagaClient j, string identifier)
        {
            var officesInCompany = Given<Company>.Match((company, facts) =>
                from office in facts.OfType<Office>()
                where office.company == company
                where !office.IsClosed

                select new
                {
                    office,
                    names = facts.Observable(
                        from name in facts.OfType<OfficeName>()
                        where name.office == office &&
                            !facts.Any<OfficeName>(next => next.prior.Contains(name))
                        select name.value
                    )
                }
            );

            var company = new Company(identifier);
            var watch = j.Watch(officesInCompany, company, projection =>
            {
                var officeViewModel = new OfficeViewModel
                {
                    Office = projection.office
                };
                Offices.Add(officeViewModel);

                projection.names.OnAdded(name =>
                {
                    officeViewModel.Name = name;
                });

                return () =>
                {
                    Offices.Remove(officeViewModel);
                };
            });
            return watch;
        }
    }
}