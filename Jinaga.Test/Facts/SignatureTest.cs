using System.Runtime.CompilerServices;
using Jinaga.Cryptography;
using Jinaga.Facts;
using Jinaga.Serialization;

namespace Jinaga.Test.Facts;

public class SignatureTest
{
    [Fact]
    public void GeneratesConsistentSignature()
    {
        KeyPair keyPair = new KeyPair(
            "-----BEGIN PUBLIC KEY-----\r\nMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4wP7IyUZICcZ5JC+UUxB\r\nZZOo8mE7R2zj8Zba5krMAqDMFbQ8bWS+nTbFVHgun1Z+5HUCZ9HHv7d7KPLu+zuI\r\nfBi5CuiJy4LJkIUuL2eRBvy8VJPeyDfvRuZ6Dc5r+vp25omx5bWbPtjPczatUphl\r\nQ83GXvITQ4ZQN/C8w7/cewq4/qVrT+TfwvIiynBSFbU5NXE6dmbE1PbJFjtBlJJ+\r\nM2uiTKMKgrC7hpluEdO3oz1itV3CTHo4DGChARLia/ZRGTUlheunbSOnFupl/Rts\r\ny/wfvEh+CBt2MduUFBo2pLCe6NMTlhEpC+/jOhQnIaU8NWy5aUh6D6pIDGwond9Y\r\nCwIDAQAB\r\n-----END PUBLIC KEY-----\r\n",
            "-----BEGIN RSA PRIVATE KEY-----\r\nMIIEpQIBAAKCAQEA4wP7IyUZICcZ5JC+UUxBZZOo8mE7R2zj8Zba5krMAqDMFbQ8\r\nbWS+nTbFVHgun1Z+5HUCZ9HHv7d7KPLu+zuIfBi5CuiJy4LJkIUuL2eRBvy8VJPe\r\nyDfvRuZ6Dc5r+vp25omx5bWbPtjPczatUphlQ83GXvITQ4ZQN/C8w7/cewq4/qVr\r\nT+TfwvIiynBSFbU5NXE6dmbE1PbJFjtBlJJ+M2uiTKMKgrC7hpluEdO3oz1itV3C\r\nTHo4DGChARLia/ZRGTUlheunbSOnFupl/Rtsy/wfvEh+CBt2MduUFBo2pLCe6NMT\r\nlhEpC+/jOhQnIaU8NWy5aUh6D6pIDGwond9YCwIDAQABAoIBAB8Ei7tdFcZFYW3P\r\n8xkTlLnmx4Y6j8luEOURCh6+KIrRYqEyi7Ecu0iq06J7e09NF7BqZmY+DQ9eaAcL\r\nzmhoVXkzPZFGfZFfcN/8undCrNeqD6d0vtNXhSuIUTPyuOFFeJp+RN7QhgI7yHiD\r\nB4KKDQgLJSgS5lvrDanfDEOowtzSs9Q0TS9dJzzJy4D/UddrSauducdEn2sAbx60\r\nUJ6JcAJcQjIi7T/AJLrkFOMrc23DUDeucR/qgRgx7BadU7TuPpbE1Phtrlryg6pi\r\n95V8fR1qRXVgVDKd69ky8HGWVcGVxyuvhzp2+JLOgjokf8vPtRdxfxlQQVfHzZDt\r\nqpDQDPUCgYEA+LbfXPvKVDj++exLZ/sn+0dPWGIE1IXPAyZNVfVxmOP/Wg3yOuwI\r\nNIYiZruP66ZxFGY7DPO198DRxn4FmSxMeDhdyYHkmJOOuT1f5Q5dJKPBW+oS0Elg\r\nd1EdrYouNNk5L1hN4JJ1CKe8lqnvgo0M7Rt+iAHtr0PKtVuNfTWBnO0CgYEA6apj\r\nOh9C94y42BTMDNxwQAjEFfCxKvEPGPD9MSPSltbbCMfFdJWwecOeY0H0/Q3qe2w0\r\nvUFy1/8yRjvwcei9dWq2vzKfjsvVjR8uE4nHfBAs2IBD9O7im0yOkUHQAjKDuMSE\r\nmfLsKQgZQOiiQ72euuCfDrdocF5Q8m3Dy8yyIdcCgYEA7jS32SaOsfukuVlHH1+8\r\n+z1hERVP6vv9ONcGWr2o/vXfKzEQPr6xXRza9enN/bR7uT3wcIc6UP+r6p/oXLvA\r\nwaO6r7RobHlmyKOvpIINU3pDRvT47+RXL+/QrNUbTCKAUogQjnW3AYMlbGd1rWPK\r\nbY1XsoumSaZ0Dx6QdMs6SEECgYEAl91XtFTRD1b0Y+vQWqOCiPuphaDzZLdbWph1\r\n1lQz8DkgDmrYGFeZZOoQrO4XLci3lxPSixZYb626nQ8jzMS5LfD3aPib3xD3cErN\r\nhYFMl4NjwipLAIup18k/94RQjr0KAzImBHBvsJNE5nzLyT8aRNbsSYJGbJHABm/0\r\niyY0t+0CgYEArmBe1THrvDmhjXsOeLBNX5n+e4lDffQ8CAWqwCthQT0FhQAu1cp9\r\nApgmMSSGjvWEvZDqeLdIXp8mVMoDDQWg38oiWoUCKl4yK87cR6PJcu38eJPixYW3\r\nzBc0D/fIthqccFxz5cKe2WzFbJKQW2q2VtZ35/WTAgeLueR9ewoFY60=\r\n-----END RSA PRIVATE KEY-----\r\n"
        );

        FactGraph graph = Serialize(new Environment("production"), keyPair);
        graph.Last.Hash.Should().Be("ZNjCylVlB7LbZqsyySE9HRNDbkv2k1KyGpBag7zIhDPnaKCHSxcU3ZH6D+GRJdv1SJ7Rk1d5a+PC+MXUzpPBWg==");

        FactSignature signature = graph.GetSignatures(graph.Last).Should().ContainSingle().Subject;
        signature.PublicKey.Should().Be(keyPair.PublicKey);
        signature.Signature.Should().Be("bfbj+2E49gqpL2A3ihvt6ybLJjrgJYCWzhjHb56F9QNLDe+K5h+NGLpCwXKMOI/gQPY7nkRW5snbugvq2C2vTTEpAdE7kEMKsg4fId+ujEwB4w+N9cXAlOr9mLAEDxZ2/pxI+BeF3BZiqnp72AY8VHE/gVMcmUcaIfgFXw7TWKrXUQ9/tJXp5N3Ph8QBH0j9L9+/GFQrquXg8M2MYmkidp+fL8tuiIMQSryCUuX4xMCTmooyTB0o2XJE6KpoJwEBQRv+FhJJGDqdaAoawNIoBEIVn5gwx7UGkJ53KgYQzL4IPSTW9OxiembNc8E7aYfyMhSG1+wFl45xpJThRuFRcA==");
    }

    private readonly ConditionalWeakTable<object, FactGraph> graphByFact = new();

    private FactGraph Serialize(object runtimeFact, KeyPair keyPair)
    {
        var collector = new Collector(SerializerCache.Empty, graphByFact, keyPair);
        var reference = collector.Serialize(runtimeFact);
        return collector.Graph;
    }
}

[FactType("MyApplication.Environment")]
internal record Environment(string identifier);