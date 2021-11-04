using System;

namespace Jinaga.Test.Model.DWS
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

}
