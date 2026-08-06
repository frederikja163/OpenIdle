using System;

namespace Generator.Core;

public interface IDtoEmitter : IDisposable
{
    void EmitDtos(DtoModel model);
}
