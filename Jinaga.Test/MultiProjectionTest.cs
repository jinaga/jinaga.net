using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace Jinaga.Test
{
    [FactType("DWS.Supplier")]
    public record Supplier(string publicKey);


    [FactType("DWS.Client")]
    public record Client(Supplier supplier, DateTime created);

    [FactType("DWS.Client.Name")]
    public record ClientName(Client client, string name, ClientName[] prior);


    [FactType("DWS.Yard")]
    public record Yard(Client client, DateTime created);

    [FactType("DWS.Yard.Address")]
    public record YardAddress(Yard yard, string name, string remark, string street, string housNb, string postalCode, string city, string country, YardAddress[] prior);

    public class MultiProjectionTest
    {
        [Fact]
        public void MultiProjection_Specify()
        {
            var addressesOfYard = Given<Yard>.Match((yard, facts) =>
                from yardAddress in facts.OfType<YardAddress>()
                where yardAddress.yard == yard
                where !(
                    from next in facts.OfType<YardAddress>()
                    where next.prior.Contains(yardAddress)
                    select next
                ).Any()
                select yardAddress
            );

            var namesOfClient = Given<Client>.Match((client, facts) =>
                from clientName in facts.OfType<ClientName>()
                where clientName.client == client
                where !(
                    from next in facts.OfType<ClientName>()
                    where next.prior.Contains(clientName)
                    select next
                ).Any()
                select clientName
            );

            var YardsAddressesWithClientsForSupplier = Given<Supplier>.Match((supplier, facts) =>
                from yard in facts.OfType<Yard>()
                where yard.client.supplier == supplier
                select new
                {
                    yard,
                    yardAddresses = facts.All(yard, addressesOfYard),
                    yard.client,
                    clientNames = facts.All(yard.client, namesOfClient)
                }
            );

            var actual = YardsAddressesWithClientsForSupplier.Pipeline.ToDescriptiveString();
            actual.Should().Be(@"supplier: DWS.Supplier {
    yard: DWS.Yard = supplier S.supplier DWS.Client S.client DWS.Yard
    client: DWS.Client = yard P.client DWS.Client
}
");
        }
    }
}
