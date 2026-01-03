using System;

namespace ExcellCore.Domain.Services;

public interface ISequentialGuidGenerator
{
    Guid Create();
}
