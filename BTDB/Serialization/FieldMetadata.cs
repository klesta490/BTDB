using System;

namespace BTDB.Serialization;

public class FieldMetadata
{
    public string Name;
    public string? DependencyName;
    public (string? IndexName, int Order)[]? Indexes;
    public Type Type;
    public uint? ByteOffset;
    public unsafe delegate*<object, object> PropObjGetter;
    public unsafe delegate*<object, ref byte, void> PropRefGetter;
    public unsafe delegate*<object, object, void> PropObjSetter;
    public unsafe delegate*<object, ref byte, void> PropRefSetter;
}