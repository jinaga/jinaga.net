using FluentAssertions;
using Jinaga.Observers;
using Jinaga.Test.Model.DWS;
using Jinaga.UnitTest;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Jinaga.Test
{
    public record ClientData(Client client, IObservableCollection<ClientName>  clientNames, IObservableCollection<YardData> yardData);
    public record YardData(Yard yard, IObservableCollection<YardAddress> yardAddresses);


    public class Specifications
    {

        public static Specification<Client, ClientName> NamesOfClient()
        {
            return Given<Client>.Match((client, facts) =>
                from clientName in facts.OfType<ClientName>()
                where clientName.client == client
                where clientName.IsCurrent
                select clientName
            );
        }


        public static Specification<Yard, YardAddress> AddressesOfYard()
        {
            return Given<Yard>.Match((yard, facts) =>
                from yardAddress in facts.OfType<YardAddress>()
                where yardAddress.yard == yard
                where yardAddress.IsCurrent
                select yardAddress
            );
        }


        public static Specification<Client, YardData> ClientYards()
        {
            return Given<Client>.Match((client, facts) =>
                from yard in facts.OfType<Yard>()
                where yard.client == client
                select new YardData
                (
                    yard,
                    facts.Observable(yard, AddressesOfYard())
                )
            );
        }


        public static Specification<Supplier, ClientData> ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier()
        {
            return Given<Supplier>.Match((supplier, facts) =>
                from client in facts.OfType<Client>()
                where client.supplier == supplier           
                select new ClientData
                (
                    client,
                    facts.Observable(client, NamesOfClient()),
                    facts.Observable(client, ClientYards())
                )
            );
        }
    }
    public record ClientYardData(Client client, string clientName, Yard yard, string yardAddress);

    public static class Transforms
    {
        public static IEnumerable<ClientYardData> FlattenClientYardData(IEnumerable<ClientData> clientsWithTheirNamesAndTheirYardsWithAddressesForSupplier)
        {
            return
                from clientData in clientsWithTheirNamesAndTheirYardsWithAddressesForSupplier
                from yardData in clientData.yardData.DefaultIfEmpty()
                select new ClientYardData(
                    clientData.client,
                    clientData.clientNames.Select(n => n.name).FirstOrDefault(),
                    yardData?.yard,
                    yardData?.yardAddresses.Select(n => AddressToString(n)).FirstOrDefault()
                );
        }

        public static string AddressToString(YardAddress n) => $"{n.street} {n.housNb} ...";
    }

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
                from client in facts.OfType<Client>()
                where client == yard.client
                select new
                {
                    yard,
                    yardAddresses = facts.Observable(yard, specifications.addressesOfYard),
                    client,
                    clientNames = facts.Observable(client, specifications.namesOfClient)
                }
            );

            var actual = YardsAddressesWithClientsForSupplier.ToDescriptiveString();
            actual.Should().Be(@"(supplier: DWS.Supplier) {
    yard: DWS.Yard [
        yard->client: DWS.Client->supplier: DWS.Supplier = supplier
    ]
    client: DWS.Client [
        client = yard->client: DWS.Client
    ]
} => {
    client = client
    clientNames = {
        clientName: DWS.Client.Name [
            clientName->client: DWS.Client = client
            !E {
                next: DWS.Client.Name [
                    next->prior: DWS.Client.Name = clientName
                ]
            }
        ]
    } => clientName
    yard = yard
    yardAddresses = {
        yardAddress: DWS.Yard.Address [
            yardAddress->yard: DWS.Yard = yard
            !E {
                next: DWS.Yard.Address [
                    next->prior: DWS.Yard.Address = yardAddress
                ]
            }
        ]
    } => yardAddress
}
".Replace("\r", ""));
        }

        [Fact]
        public  async System.Threading.Tasks.Task GetClientsWithTheirNamesAndTheirYardsWithAddressesForSupplierTest()
        {
            var j = JinagaTest.Create();

            var supplier1 = await j.Fact(new Supplier("---Sup01PubKey---"));
            var supplier2 = await j.Fact(new Supplier("---Sup02PubKey---"));
            var supplier3 = await j.Fact(new Supplier("---Sup03PubKey---"));

            var client1_S1 = await j.Fact(new Client(supplier1, DateTime.Now.AddSeconds(-1.0)));
            var client2_S1 = await j.Fact(new Client(supplier1, DateTime.Now));
            var client1_S2 = await j.Fact(new Client(supplier2, DateTime.Now));
            var client1_S3 = await j.Fact(new Client(supplier3, DateTime.Now));

            var name1A_C1_S1 = await j.Fact(new ClientName(client1_S1, "N1A_C1_S1", new ClientName[] { }));
            var name1B_C1_S1 = await j.Fact(new ClientName(client1_S1, "N1B_C1_S1", new ClientName[] { }));
            var name2_C1_S1 = await j.Fact(new ClientName(client1_S1, "N2_C1_S1", new ClientName[] { name1A_C1_S1, name1B_C1_S1 }));

            var name1_C2_S1 = await j.Fact(new ClientName(client2_S1, "N1_C2_S1", new ClientName[] { }));
            var name2A_C2_S1 = await j.Fact(new ClientName(client2_S1, "N2A_C2_S1", new ClientName[] { name1_C2_S1 }));
            var name2B_C2_S1 = await j.Fact(new ClientName(client2_S1, "N2B_C2_S1", new ClientName[] { name1_C2_S1 }));

            var name1_C1_S2 = await j.Fact(new ClientName(client1_S2, "N1_C1_S2", new ClientName[] { }));

            var name1_C1_S3 = await j.Fact(new ClientName(client1_S3, "N1_C1_S3", new ClientName[] { }));

            var yard1_C1_S1 = await j.Fact(new Yard(client1_S1, DateTime.Now.AddSeconds(-1.0)));
            var yard2_C1_S1 = await j.Fact(new Yard(client1_S1, DateTime.Now));
            var yard1_C2_S1 = await j.Fact(new Yard(client2_S1, DateTime.Now));
            var yard1_C1_S2 = await j.Fact(new Yard(client1_S2, DateTime.Now));

            var address1_Y1_C1_S1 = await j.Fact(new YardAddress(yard1_C1_S1, "Name_Y1_C1_S1", "Gate is locked. Call owner (Tel: 0612345678) for code.", "TestStreet", "12C", "123456ABC", "TestCity", "France", new YardAddress[] { }));
            var address2_Y1_C1_S1 = await j.Fact(new YardAddress(yard1_C1_S1, "Name_Y1_C1_S1", "Gate is locked. Call owner (Tel: 0612345678) for code.", "TestStreet", "12B", "123456ABC", "TestCity", "France", new YardAddress[] { address1_Y1_C1_S1 }));

            var address1_Y2_C1_S1 = await j.Fact(new YardAddress(yard2_C1_S1, "Name_Y2_C1_S1", "Remark for address1_Y2_C1_S1", "Corneel Franckstraat", "24", "2100", "Deurne", "België", new YardAddress[] { }));
            var address2A_Y2_C1_S1 = await j.Fact(new YardAddress(yard2_C1_S1, "Name_Y2_C1_S1", "Remark for address2A_Y2_C1_S1", "Brusselstraat", "15", "2018", "Antwerpen", "België", new YardAddress[] { address1_Y2_C1_S1 }));
            var address2B_Y2_C1_S1 = await j.Fact(new YardAddress(yard2_C1_S1, "Name_Y2_C1_S1", "Remark for address2B_Y2_C1_S1", "Brusselstraat", "51", "2018", "Antwerpen", "Belgium", new YardAddress[] { address1_Y2_C1_S1 }));

            var address1_Y1_C2_S1 = await j.Fact(new YardAddress(yard1_C2_S1, "Name_Y1_C2_S1", "Remark for address1_Y1_C2_S1", "TestStreet", "99", "1234A", "Paris", "France", new YardAddress[] { }));
            
            var address1_Y1_C1_S2 = await j.Fact(new YardAddress(yard1_C1_S2, "Name_Y1_C1_S2", "Remark for address1_Y1_C1_S2", "AnotherStreet", "547B", "66740", "Place", "Belgique", new YardAddress[] { }));


            ImmutableList<ClientData> ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier1 = await j.Query(supplier1, Specifications.ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier());
            
            var flattened = Transforms.FlattenClientYardData(ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier1);
            flattened.Should().BeEquivalentTo(
                new[] {
                    new {
                        client = client1_S1,
                        // clientNames = new[] {name2_C1_S1},
                        clientName = name2_C1_S1.name,
                        yard = yard1_C1_S1,
                        yardAddress = Transforms.AddressToString(address2_Y1_C1_S1)
                        // yardAddresses = new[] { address2_Y1_C1_S1}
                    },
                    new {
                        client = client1_S1,
                        clientName = name2_C1_S1.name,
                        // clientNames = new[] {name2_C1_S1},
                        yard = yard2_C1_S1,
                        yardAddress = Transforms.AddressToString(address2A_Y2_C1_S1)
                        // yardAddresses = new[] { address2A_Y2_C1_S1, address2B_Y2_C1_S1 }
                    },
                    new {
                        client = client2_S1,
                        clientName = name2A_C2_S1.name,
                        // clientNames = new[] {name2A_C2_S1, name2B_C2_S1},
                        yard = yard1_C2_S1,
                        yardAddress = Transforms.AddressToString(address1_Y1_C2_S1)
                        // yardAddresses = new[] { address1_Y1_C2_S1 }
                    }
                }
            );



            // ImmutableList<ClientData> ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier2 = await j.Query(supplier2, Specifications.ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier());
            
            // ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier2.Should().BeEquivalentTo(
            //     new[] {
            //         new {
            //             client = client1_S2,
            //             clientNames = new[] {name1_C1_S2},
            //             yard = yard1_C1_S2,
            //             yardAddresses = new[] { address1_Y1_C1_S2 }
            //         }
            //     }
            // );



            // ImmutableList<ClientData> ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier3 = await j.Query(supplier3, Specifications.ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier());
            
            // ClientsWithTheirNamesAndTheirYardsWithAddressesForSupplier3.Should().BeEquivalentTo(
            //    new[] {
            //        new {
            //            client = client1_S3,
            //            clientNames = new[] {name1_C1_S3},
            //            yard = (Yard)null,
            //            yardAddresses = Array.Empty<object>()
            //        }
            //    }
            // );
        
        }
    }
}
