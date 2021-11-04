using FluentAssertions;
using Jinaga.Test.Model.DWS;
using System;
using System.Linq;
using Xunit;

namespace Jinaga.Test
{
    public class MultiProjectionTest
    {
        private static Specification<Yard, YardAddress> addressesOfYard = Given<Yard>.Match((yard, facts) =>
            from yardAddress in facts.OfType<YardAddress>()
            where yardAddress.yard == yard
            where !(
                from next in facts.OfType<YardAddress>()
                where next.prior.Contains(yardAddress)
                select next
            ).Any()
            select yardAddress
        );

        private static Specification<Client, ClientName> namesOfClient = Given<Client>.Match((client, facts) =>
            from clientName in facts.OfType<ClientName>()
            where clientName.client == client
            where !(
                from next in facts.OfType<ClientName>()
                where next.prior.Contains(clientName)
                select next
            ).Any()
            select clientName
        );

        [Fact]
        public void MultiProjection_Specify()
        {
            var specifications = new
            {
                addressesOfYard,
                namesOfClient
            };

            var YardsAddressesWithClientsForSupplier = Given<Supplier>.Match((supplier, facts) =>
                from yard in facts.OfType<Yard>()
                where yard.client.supplier == supplier
                select new
                {
                    yard,
                    yardAddresses = facts.All(yard, specifications.addressesOfYard),
                    yard.client,
                    clientNames = facts.All(yard.client, specifications.namesOfClient)
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
