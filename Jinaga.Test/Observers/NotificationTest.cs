using FluentAssertions;
using Jinaga.Test.Model;
using Jinaga.UnitTest;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Jinaga.Test.Observers;
public class NotificationTest
{
    [Fact]
    public async Task NotifyOfficeOpened()
    {
        var jinagaClient = GivenJinagaClient();

        var company = await jinagaClient.Fact(new Company("contoso"));
        var dallas = await jinagaClient.Fact(new City("Dallas"));

        var viewModel = new CompanyViewModel();
        var observer = viewModel.Start(jinagaClient, company);
        await observer.Loaded;

        try
        {
            var office = await jinagaClient.Fact(new Office(company, dallas));
            await jinagaClient.Fact(new OfficeName(office, "Dallas", new OfficeName[0]));

            viewModel.Offices.Should().ContainSingle().Which
                .Name.Should().Be("Dallas");
        }
        finally
        {
            observer.Stop();
        }
    }

    [Fact]
    public async Task NotifyExistingOffice()
    {
        var jinagaClient = GivenJinagaClient();

        var company = await jinagaClient.Fact(new Company("contoso"));
        var dallas = await jinagaClient.Fact(new City("Dallas"));
        var office = await jinagaClient.Fact(new Office(company, dallas));
        await jinagaClient.Fact(new OfficeName(office, "Dallas", new OfficeName[0]));

        var viewModel = new CompanyViewModel();
        var observer = viewModel.Start(jinagaClient, company);
        await observer.Loaded;

        try
        {
            viewModel.Offices.Should().ContainSingle().Which
                .Name.Should().Be("Dallas");
        }
        finally
        {
            observer.Stop();
        }
    }

    [Fact]
    public async Task NotifyOfficeClosed()
    {
        var jinagaClient = GivenJinagaClient();

        var company = await jinagaClient.Fact(new Company("contoso"));
        var dallas = await jinagaClient.Fact(new City("Dallas"));
        var office = await jinagaClient.Fact(new Office(company, dallas));
        await jinagaClient.Fact(new OfficeName(office, "Dallas", new OfficeName[0]));

        var viewModel = new CompanyViewModel();
        var observer = viewModel.Start(jinagaClient, company);
        await observer.Loaded;

        try
        {
            await jinagaClient.Fact(new OfficeClosure(office, DateTime.Now));

            viewModel.Offices.Should().BeEmpty();
        }
        finally
        {
            observer.Stop();
        }
    }

    private JinagaClient GivenJinagaClient()
    {
        return JinagaTest.Create();
    }
}

internal class OfficeViewModel
{
    public string Name { get; set; }
}

internal class CompanyViewModel
{
    private ImmutableList<OfficeViewModel> offices = ImmutableList<OfficeViewModel>.Empty;

    public IEnumerable<OfficeViewModel> Offices => offices;

    public IObserver Start(JinagaClient jinagaClient, Company company)
    {
        var officesInCompany = Given<Company>.Match((company, facts) =>
            from office in facts.OfType<Office>()
            where office.company == company &&
                !office.IsClosed
            select new
            {
                names = facts.Observable(
                    from name in facts.OfType<OfficeName>()
                    where name.office == office &&
                        !facts.Any<OfficeName>(next =>
                            next.prior.Contains(name)
                        )
                    select name.value
                )
            }
        );

        return jinagaClient.Watch(officesInCompany, company, projection =>
        {
            var officeViewModel = new OfficeViewModel();
            offices = offices.Add(officeViewModel);
            projection.names.OnAdded(name =>
            {
                officeViewModel.Name = name;
            });

            return () =>
            {
                offices = offices.Remove(officeViewModel);
            };
        });
    }
}