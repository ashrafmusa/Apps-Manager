using System;
using ExcellCore.Domain.Services;

namespace ExcellCore.Infrastructure.Services;

public sealed class SequentialGuidGenerator : ISequentialGuidGenerator
{
    public Guid Create()
    {
        // COMB-style GUID: keep randomness, add timestamp bytes for locality
        var guidBytes = Guid.NewGuid().ToByteArray();
        var timestamp = DateTime.UtcNow.Ticks;
        var timestampBytes = BitConverter.GetBytes(timestamp);

        // overwrite last 6 bytes to encode time while retaining randomness in first 10 bytes
        guidBytes[10] = timestampBytes[2];
        guidBytes[11] = timestampBytes[3];
        guidBytes[12] = timestampBytes[4];
        guidBytes[13] = timestampBytes[5];
        guidBytes[14] = timestampBytes[6];
        guidBytes[15] = timestampBytes[7];

        return new Guid(guidBytes);
    }
}
