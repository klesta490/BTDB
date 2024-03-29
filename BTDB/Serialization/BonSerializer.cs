using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using BTDB.Bon;
using BTDB.Collections;
using BTDB.IL;

namespace BTDB.Serialization;

public delegate void Serialize(ref SerializerCtx ctx, ref byte value);

public delegate bool Deserialize(ref DeserializerCtx ctx, ref byte value);

public interface ISerializerFactory
{
    void SerializeObject(ref SerializerCtx ctx, object? value);
    Serialize CreateSerializerForType(Type type);
    object? DeserializeObject(ref DeserializerCtx ctx);
    Deserialize CreateDeserializerForType(Type type);
}

public ref struct SerializerCtx
{
    public ISerializerFactory Factory;
}

public ref struct DeserializerCtx
{
    public ISerializerFactory Factory;
}

public ref struct BonSerializerCtx
{
    public ISerializerFactory Factory;
    public ref BonBuilder Builder;
}

public ref struct BonDeserializerCtx
{
    public ISerializerFactory Factory;
    public ref Bon.Bon Bon;
}

public class BonSerializerFactory : ISerializerFactory
{
    readonly ConcurrentDictionary<nint, Serialize> _cache = new();
    readonly ConcurrentDictionary<nint, Deserialize> _cache2 = new();

    public void SerializeObject(ref SerializerCtx ctx, object? obj)
    {
        if (obj == null)
        {
            AsCtx(ref ctx).Builder.WriteNull();
            return;
        }

        var type = obj.GetType();
        var typePtr = type.TypeHandle.Value;
        _cache.TryGetValue(typePtr, out var serializer);
        if (serializer == null)
        {
            serializer = CreateSerializerForType(type);
            _cache.TryAdd(typePtr, serializer);
            _cache.TryGetValue(typePtr, out serializer);
        }

        if (type.IsValueType)
        {
            serializer!(ref ctx, ref RawData.Ref(obj, (uint)Unsafe.SizeOf<nint>()));
        }
        else
        {
            _cache.TryGetValue(typeof(object).TypeHandle.Value, out var objSerializer);
            while (serializer == objSerializer)
            {
                Thread.Yield();
                _cache.TryGetValue(typePtr, out serializer);
            }

            serializer!(ref ctx, ref Unsafe.As<object, byte>(ref obj));
        }
    }

    [DebuggerStepThrough]
    static unsafe ref BonSerializerCtx AsCtx(ref SerializerCtx ctx)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        fixed (void* ptr = &ctx)
        {
            return ref *(BonSerializerCtx*)ptr;
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    [DebuggerStepThrough]
    static unsafe ref SerializerCtx AsCtx(ref BonSerializerCtx ctx)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        fixed (void* ptr = &ctx)
        {
            return ref *(SerializerCtx*)ptr;
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    [DebuggerStepThrough]
    static unsafe ref BonDeserializerCtx AsCtx(ref DeserializerCtx ctx)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        fixed (void* ptr = &ctx)
        {
            return ref *(BonDeserializerCtx*)ptr;
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    [DebuggerStepThrough]
    static unsafe ref DeserializerCtx AsCtx(ref BonDeserializerCtx ctx)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        fixed (void* ptr = &ctx)
        {
            return ref *(DeserializerCtx*)ptr;
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    public Serialize CreateCachedSerializerForType(Type type)
    {
        var typePtr = type.TypeHandle.Value;
        _cache.TryGetValue(typePtr, out var serializer);
        if (serializer == null)
        {
            if (type == typeof(object) || type.IsInterface || type == typeof(string) || type == typeof(byte[]) ||
                type.IsValueType)
            {
                serializer = CreateSerializerForType(type);
                _cache.TryAdd(typePtr, serializer);
                _cache.TryGetValue(typePtr, out serializer);
            }
            else
            {
                var objSerializer = CreateCachedSerializerForType(typeof(object));
                _cache.TryAdd(typePtr, objSerializer);
                serializer = CreateSerializerForType(type);
                _cache.TryUpdate(typePtr, serializer, objSerializer);
                _cache.TryGetValue(typePtr, out serializer);
            }
        }

        return serializer!;
    }

    public unsafe Serialize CreateSerializerForType(Type type)
    {
        if (type == typeof(object) || type.IsInterface)
        {
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Factory.SerializeObject(ref ctx, Unsafe.As<byte, object>(ref value));
            };
        }

        if (type == typeof(string))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, string>(ref value));
            };
        }

        if (type == typeof(byte[]))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, byte[]>(ref value));
            };
        }

        if (type == typeof(bool))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, bool>(ref value));
            };
        }

        if (type == typeof(DateTime))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, DateTime>(ref value));
            };
        }

        if (type == typeof(Guid))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, Guid>(ref value));
            };
        }

        if (type == typeof(sbyte))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, sbyte>(ref value));
            };
        }

        if (type == typeof(short))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, short>(ref value));
            };
        }

        if (type == typeof(int))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, int>(ref value));
            };
        }

        if (type == typeof(long))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, long>(ref value));
            };
        }

        if (type == typeof(byte))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, byte>(ref value));
            };
        }

        if (type == typeof(ushort) || type == typeof(char))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, ushort>(ref value));
            };
        }

        if (type == typeof(uint))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, uint>(ref value));
            };
        }

        if (type == typeof(ulong))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, ulong>(ref value));
            };
        }

        if (type == typeof(Half))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write((double)Unsafe.As<byte, Half>(ref value));
            };
        }

        if (type == typeof(float))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, float>(ref value));
            };
        }

        if (type == typeof(double))
        {
            return static (ref SerializerCtx ctx, ref byte value) =>
            {
                AsCtx(ref ctx).Builder.Write(Unsafe.As<byte, double>(ref value));
            };
        }

        if (type.IsEnum)
        {
            return CreateSerializerForType(Enum.GetUnderlyingType(type));
        }

        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            var offset = RawData.Align(1, RawData.GetSizeAndAlign(nullableType).Align);
            var serializer = CreateCachedSerializerForType(nullableType);
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                if (value != 0)
                {
                    serializer(ref ctx, ref Unsafe.AddByteOffset(ref value, offset));
                }
                else
                {
                    AsCtx(ref ctx).Builder.WriteNull();
                }
            };
        }

        if (type.SpecializationOf(typeof(ValueTuple<,>)) is { } valueTuple2)
        {
            var typeParams = valueTuple2.GetGenericArguments();
            var offsets = RawData.GetOffsets(typeParams[0], typeParams[1]);
            var serializer0 = CreateCachedSerializerForType(typeParams[0]);
            var serializer1 = CreateCachedSerializerForType(typeParams[1]);
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                ref var builder = ref AsCtx(ref ctx).Builder;
                builder.StartTuple();
                serializer0(ref ctx, ref Unsafe.AddByteOffset(ref value, offsets.Item1));
                serializer1(ref ctx, ref Unsafe.AddByteOffset(ref value, offsets.Item2));
                builder.FinishTuple();
            };
        }

        if (type.SpecializationOf(typeof(Tuple<,>)) is { } tuple2)
        {
            var typeParams = tuple2.GetGenericArguments();
            var offsets = RawData.GetOffsets(typeParams[0], typeParams[1]);
            offsets = (offsets.Item1 + (uint)Unsafe.SizeOf<nint>(), offsets.Item2 + (uint)Unsafe.SizeOf<nint>());
            var serializer0 = CreateCachedSerializerForType(typeParams[0]);
            var serializer1 = CreateCachedSerializerForType(typeParams[1]);
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                if (Unsafe.As<byte, object>(ref value) == null)
                {
                    AsCtx(ref ctx).Builder.WriteNull();
                    return;
                }

                ref var builder = ref AsCtx(ref ctx).Builder;
                builder.StartTuple();
                serializer0(ref ctx, ref RawData.Ref(Unsafe.As<byte, object>(ref value), offsets.Item1));
                serializer1(ref ctx, ref RawData.Ref(Unsafe.As<byte, object>(ref value), offsets.Item2));
                builder.FinishTuple();
            };
        }

        if (type.IsArray)
        {
            if (!type.IsSZArray) throw new InvalidOperationException("Only SZArray is supported");
            var elementType = type.GetElementType()!;
            var elementTypeSerializer = CreateCachedSerializerForType(elementType);
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                var obj = Unsafe.As<byte, object>(ref value);
                if (obj == null)
                {
                    AsCtx(ref ctx).Builder.WriteNull();
                    return;
                }

                var count = Unsafe.As<byte, int>(ref RawData.Ref(obj, (uint)Unsafe.SizeOf<nint>()));
                ref readonly var mt = ref RawData.MethodTableRef(obj);
                var offset = mt.BaseSize - (uint)Unsafe.SizeOf<nint>();
                var offsetDelta = mt.ComponentSize;
                AsCtx(ref ctx).Builder.StartArray();
                for (var i = 0; i < count; i++, offset += offsetDelta)
                {
                    elementTypeSerializer(ref ctx, ref RawData.Ref(obj, offset));
                }

                AsCtx(ref ctx).Builder.FinishArray();
            };
        }

        if (type.SpecializationOf(typeof(List<>)) is { } listType)
        {
            var elementType = listType.GetGenericArguments()[0];
            var elementTypeSerializer = CreateCachedSerializerForType(elementType);
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                var obj = Unsafe.As<byte, object>(ref value);
                if (obj == null)
                {
                    AsCtx(ref ctx).Builder.WriteNull();
                    return;
                }

                var count = Unsafe.As<ICollection>(obj).Count;
                obj = RawData.ListItems(Unsafe.As<List<object>>(obj));
                ref readonly var mt = ref RawData.MethodTableRef(obj);
                var offset = mt.BaseSize - (uint)Unsafe.SizeOf<nint>();
                var offsetDelta = mt.ComponentSize;
                AsCtx(ref ctx).Builder.StartArray();
                for (var i = 0; i < count; i++, offset += offsetDelta)
                {
                    elementTypeSerializer(ref ctx, ref RawData.Ref(obj, offset));
                }

                AsCtx(ref ctx).Builder.FinishArray();
            };
        }

        if (type.SpecializationOf(typeof(HashSet<>)) is { } hashSetType)
        {
            var elementType = hashSetType.GetGenericArguments()[0];
            var elementTypeSerializer = CreateCachedSerializerForType(elementType);
            var layout = RawData.GetHashSetEntriesLayout(elementType);
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                var obj = Unsafe.As<byte, object>(ref value);
                if (obj == null)
                {
                    AsCtx(ref ctx).Builder.WriteNull();
                    return;
                }

                var count = Unsafe.As<byte, int>(ref RawData.Ref(obj,
                    RawData.Align(8 + 4 * (uint)Unsafe.SizeOf<nint>(), 8)));
                obj = RawData.HashSetEntries(Unsafe.As<HashSet<object>>(obj));
                ref readonly var mt = ref RawData.MethodTableRef(obj);
                var offset = mt.BaseSize - (uint)Unsafe.SizeOf<nint>();
                var offsetDelta = mt.ComponentSize;
                Debug.Assert(offsetDelta == layout.Size);
                AsCtx(ref ctx).Builder.StartArray();
                for (var i = 0; i < count; i++, offset += offsetDelta)
                {
                    if (Unsafe.As<byte, int>(ref RawData.Ref(obj, offset + 4)) < -1)
                    {
                        continue;
                    }

                    elementTypeSerializer(ref ctx, ref RawData.Ref(obj, offset + layout.Offset));
                }

                AsCtx(ref ctx).Builder.FinishArray();
            };
        }

        if (type.SpecializationOf(typeof(Dictionary<,>)) is { } dictionaryType)
        {
            var keyType = dictionaryType.GetGenericArguments()[0];
            var valueType = dictionaryType.GetGenericArguments()[1];
            var keyTypeSerializer = CreateCachedSerializerForType(keyType);
            var valueTypeSerializer = CreateCachedSerializerForType(valueType);
            var layout = RawData.GetDictionaryEntriesLayout(keyType, valueType);
            return (ref SerializerCtx ctx, ref byte value) =>
            {
                var obj = Unsafe.As<byte, object>(ref value);
                if (obj == null)
                {
                    AsCtx(ref ctx).Builder.WriteNull();
                    return;
                }

                var count = Unsafe.As<byte, int>(ref RawData.Ref(obj,
                    RawData.Align(8 + 6 * (uint)Unsafe.SizeOf<nint>(), 8)));
                obj = RawData.HashSetEntries(Unsafe.As<HashSet<object>>(obj));
                ref readonly var mt = ref RawData.MethodTableRef(obj);
                var offset = mt.BaseSize - (uint)Unsafe.SizeOf<nint>();
                var offsetDelta = mt.ComponentSize;
                AsCtx(ref ctx).Builder.StartDictionary();
                for (var i = 0; i < count; i++, offset += offsetDelta)
                {
                    if (Unsafe.As<byte, int>(ref RawData.Ref(obj, offset + 4)) < -1)
                    {
                        continue;
                    }

                    keyTypeSerializer(ref ctx, ref RawData.Ref(obj, offset + layout.OffsetKey));
                    valueTypeSerializer(ref ctx, ref RawData.Ref(obj, offset + layout.OffsetValue));
                }

                AsCtx(ref ctx).Builder.FinishDictionary();
            };
        }

        var classMetadata = ReflectionMetadata.FindByType(type);
        if (classMetadata != null)
        {
            var persistName = classMetadata.TruePersistedName;
            var persistNameUtf8 = Encoding.UTF8.GetBytes(persistName);
            var fieldSerializers = new Serialize[classMetadata.Fields.Length];
            for (var i = 0; i < classMetadata.Fields.Length; i++)
            {
                var field = classMetadata.Fields[i];
                var nameUtf8 = Encoding.UTF8.GetBytes(field.Name);
                var serializer = CreateCachedSerializerForType(field.Type);
                if (field.PropRefGetter != null)
                {
                    var getter = field.PropRefGetter;
                    if (field.Type.IsValueType)
                    {
                        if ((*(RawData.MethodTable*)field.Type.TypeHandle.Value).ContainsGCPointers)
                            throw new InvalidOperationException("Value type with GC pointers is not supported.");
                        fieldSerializers[i] = (ref SerializerCtx ctx, ref byte value) =>
                        {
                            AsCtx(ref ctx).Builder.WriteKey(nameUtf8);
                            UInt128 temp = default;
                            getter(Unsafe.As<byte, object>(ref value), ref Unsafe.As<UInt128, byte>(ref temp));
                            serializer(ref ctx, ref Unsafe.As<UInt128, byte>(ref temp));
                        };
                    }
                    else
                    {
                        fieldSerializers[i] = (ref SerializerCtx ctx, ref byte value) =>
                        {
                            AsCtx(ref ctx).Builder.WriteKey(nameUtf8);
                            object? tempObject = null;
                            getter(Unsafe.As<byte, object>(ref value), ref Unsafe.As<object, byte>(ref tempObject));
                            serializer(ref ctx, ref Unsafe.As<object, byte>(ref tempObject));
                        };
                    }
                }
                else
                {
                    var offset = field.ByteOffset!.Value;
                    fieldSerializers[i] = (ref SerializerCtx ctx, ref byte value) =>
                    {
                        AsCtx(ref ctx).Builder.WriteKey(nameUtf8);
                        serializer(ref ctx, ref RawData.Ref(Unsafe.As<byte, object>(ref value), offset));
                    };
                }
            }

            return (ref SerializerCtx ctx, ref byte value) =>
            {
                if (Unsafe.As<byte, object>(ref value) == null)
                {
                    AsCtx(ref ctx).Builder.WriteNull();
                    return;
                }

                AsCtx(ref ctx).Builder.StartClass(persistNameUtf8);
                for (var i = 0; i < fieldSerializers.Length; i++)
                {
                    fieldSerializers[i](ref ctx, ref value);
                }

                AsCtx(ref ctx).Builder.FinishClass();
            };
        }

        throw new NotSupportedException("BonSerialization of " + type.ToSimpleName() + " is not supported.");
    }

    public Deserialize CreateCachedDeserializerForType(Type type)
    {
        var typePtr = type.TypeHandle.Value;
        _cache2.TryGetValue(typePtr, out var deserializer);
        if (deserializer == null)
        {
            deserializer = CreateDeserializerForType(type);
            _cache2.TryAdd(typePtr, deserializer);
            _cache2.TryGetValue(typePtr, out deserializer);
        }

        return deserializer!;
    }

    public object? DeserializeObject(ref DeserializerCtx ctx)
    {
        switch (AsCtx(ref ctx).Bon.BonType)
        {
            case BonType.Null:
            case BonType.Undefined:
                return null;
            case BonType.Class:
            {
                AsCtx(ref ctx).Bon.PeekClass(out var name);
                var deserializer =
                    ((BonSerializerFactory)AsCtx(ref ctx).Factory).CreateCachedDeserializerForName(name);
                object? res = null;
                deserializer(ref ctx, ref Unsafe.As<object, byte>(ref res));
                return res;
            }
            case BonType.Array:
            {
                AsCtx(ref ctx).Bon.TryGetArray(out var arrayBon);
                var count = arrayBon.Items;
                var res = new object?[count];
                for (var idx = 0u; idx < count; idx++)
                {
                    arrayBon.TryGet(idx, out var itemBon);
                    BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref itemBon };
                    res[idx] = AsCtx(ref ctx).Factory.DeserializeObject(ref AsCtx(ref subCtx));
                }

                return res;
            }
            case BonType.Tuple:
            {
                AsCtx(ref ctx).Bon.TryGetTuple(out var arrayBon);
                arrayBon.TryGet(0, out var itemBon);
                var count = arrayBon.Items;
                if (count == 2)
                {
                    BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref itemBon };
                    var i0 = AsCtx(ref ctx).Factory.DeserializeObject(ref AsCtx(ref subCtx));
                    var i1 = AsCtx(ref ctx).Factory.DeserializeObject(ref AsCtx(ref subCtx));
                    var res = new Tuple<object?, object?>(i0, i1);
                    return res;
                }

                throw new NotSupportedException("Tuple with " + count + " items is not supported.");
            }
            case BonType.Dictionary:
            {
                AsCtx(ref ctx).Bon.TryGetDictionary(out var dictBon);
                var res = new Dictionary<object?, object?>();
                BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref dictBon };
                while (dictBon.Items > 0)
                {
                    var keyObj = AsCtx(ref ctx).Factory.DeserializeObject(ref AsCtx(ref subCtx));
                    var valueObj = AsCtx(ref ctx).Factory.DeserializeObject(ref AsCtx(ref subCtx));
                    if (keyObj != null)
                        res[keyObj] = valueObj;
                }

                return res;
            }
            case BonType.Bool:
            {
                AsCtx(ref ctx).Bon.TryGetBool(out var res);
                return res;
            }
            case BonType.String:
            {
                AsCtx(ref ctx).Bon.TryGetString(out var res);
                return res;
            }
            case BonType.Float:
            {
                AsCtx(ref ctx).Bon.TryGetDouble(out var res);
                return res;
            }
            case BonType.DateTime:
            {
                AsCtx(ref ctx).Bon.TryGetDateTime(out var res);
                return res;
            }
            case BonType.Guid:
            {
                AsCtx(ref ctx).Bon.TryGetGuid(out var res);
                return res;
            }
            case BonType.ByteArray:
            {
                AsCtx(ref ctx).Bon.TryGetByteArray(out var res);
                return res.ToArray();
            }
            case BonType.Integer:
            {
                if (AsCtx(ref ctx).Bon.TryGetULong(out var res2))
                    return res2;
                AsCtx(ref ctx).Bon.TryGetLong(out var res);
                return res;
            }
            case BonType.Object:
            {
                AsCtx(ref ctx).Bon.TryGetObject(out var keyedBon);
                var res = new Dictionary<string, object?>((int)keyedBon.Items);
                var valuesBon = keyedBon.Values();
                BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref valuesBon };
                while (keyedBon.NextKey() is { } key)
                {
                    res[key] = AsCtx(ref ctx).Factory.DeserializeObject(ref AsCtx(ref subCtx));
                }

                return res;
            }
            default:
                throw new InvalidDataException("Cannot deserialize BonType " + AsCtx(ref ctx).Bon.BonType);
        }
    }

    public Deserialize CreateCachedDeserializerForName(ReadOnlySpan<byte> name)
    {
        if (ReflectionMetadata.FindByName(name) is { } classMetadata)
        {
            var type = classMetadata.Type;
            var typePtr = type.TypeHandle.Value;
            _cache2.TryGetValue(typePtr, out var deserializer);
            if (deserializer == null)
            {
                if (type == typeof(object) || type.IsInterface || type == typeof(string) || type == typeof(byte[]) ||
                    type.IsValueType)
                {
                    deserializer = CreateDeserializerForType(type);
                    _cache2.TryAdd(typePtr, deserializer);
                    _cache2.TryGetValue(typePtr, out deserializer);
                }
                else
                {
                    Deserialize objDeserializer = (ref DeserializerCtx ctx, ref byte value) =>
                    {
                        var res = AsCtx(ref ctx).Factory.DeserializeObject(ref ctx);
                        if (res == null || type.IsInstanceOfType(res))
                        {
                            Unsafe.As<byte, object>(ref value) = res;
                        }
                        else
                        {
                            Unsafe.As<byte, object>(ref value) = null;
                        }

                        return true;
                    };
                    _cache2.TryAdd(typePtr, objDeserializer);
                    deserializer = CreateDeserializerForType(type);
                    _cache2.TryUpdate(typePtr, deserializer, objDeserializer);
                }
            }

            return deserializer!;
        }

        return (ref DeserializerCtx ctx, ref byte _) =>
        {
            AsCtx(ref ctx).Bon.Skip();
            return false;
        };
    }

    public unsafe Deserialize CreateDeserializerForType(Type type)
    {
        if (type == typeof(object))
        {
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                Unsafe.As<byte, object>(ref value) = AsCtx(ref ctx).Factory.DeserializeObject(ref ctx);
                return true;
            };
        }

        if (type.IsInterface)
        {
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                var res = AsCtx(ref ctx).Factory.DeserializeObject(ref ctx);
                if (res == null || type.IsInstanceOfType(res))
                {
                    Unsafe.As<byte, object>(ref value) = res;
                }
                else
                {
                    Unsafe.As<byte, object>(ref value) = null;
                }

                return true;
            };
        }

        if (type == typeof(string))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetString(out var v))
                {
                    Unsafe.As<byte, string>(ref value) = v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(byte[]))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetByteArray(out var v))
                {
                    Unsafe.As<byte, byte[]>(ref value) = v.ToArray();
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(byte))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetULong(out var v))
                {
                    value = (byte)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(sbyte))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetLong(out var v))
                {
                    Unsafe.As<byte, sbyte>(ref value) = (sbyte)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(ushort))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetULong(out var v))
                {
                    Unsafe.As<byte, ushort>(ref value) = (ushort)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(short))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetLong(out var v))
                {
                    Unsafe.As<byte, short>(ref value) = (short)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(uint))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetULong(out var v))
                {
                    Unsafe.As<byte, uint>(ref value) = (uint)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(int))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetLong(out var v))
                {
                    Unsafe.As<byte, int>(ref value) = (int)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(ulong))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetULong(out var v))
                {
                    Unsafe.As<byte, ulong>(ref value) = v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(long))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetLong(out var v))
                {
                    Unsafe.As<byte, long>(ref value) = v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(float))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetDouble(out var v))
                {
                    Unsafe.As<byte, float>(ref value) = (float)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(double))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetDouble(out var v))
                {
                    Unsafe.As<byte, double>(ref value) = v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(bool))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetBool(out var v))
                {
                    Unsafe.As<byte, bool>(ref value) = v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(DateTime))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetDateTime(out var v))
                {
                    Unsafe.As<byte, DateTime>(ref value) = v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(Guid))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetGuid(out var v))
                {
                    Unsafe.As<byte, Guid>(ref value) = v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type == typeof(Half))
        {
            return static (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetDouble(out var v))
                {
                    Unsafe.As<byte, Half>(ref value) = (Half)v;
                    return true;
                }
                else
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }
            };
        }

        if (type.IsEnum)
        {
            return CreateDeserializerForType(Enum.GetUnderlyingType(type));
        }

        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            var offset = RawData.Align(1, RawData.GetSizeAndAlign(nullableType).Align);
            var deserializer = CreateCachedDeserializerForType(nullableType);
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetNull())
                {
                    value = 0;
                    return true;
                }

                if (deserializer(ref ctx, ref Unsafe.AddByteOffset(ref value, offset)))
                {
                    value = 1;
                    return true;
                }

                return false;
            };
        }

        if (type.SpecializationOf(typeof(ValueTuple<,>)) is { } valueTuple2)
        {
            var typeParams = valueTuple2.GetGenericArguments();
            var offsets = RawData.GetOffsets(typeParams[0], typeParams[1]);
            var deserializer0 = CreateCachedDeserializerForType(typeParams[0]);
            var deserializer1 = CreateCachedDeserializerForType(typeParams[1]);
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetTuple(out var tupleBon) && tupleBon.Items >= 2)
                {
                    tupleBon.TryGet(0, out var itemBon);
                    BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref itemBon };
                    if (deserializer0(ref AsCtx(ref subCtx), ref Unsafe.AddByteOffset(ref value, offsets.Item1)))
                    {
                        tupleBon.TryGet(1, out itemBon);
                        if (deserializer1(ref AsCtx(ref subCtx), ref Unsafe.AddByteOffset(ref value, offsets.Item2)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            };
        }

        if (type.SpecializationOf(typeof(Tuple<,>)) is { } tuple2)
        {
            var typeParams = tuple2.GetGenericArguments();
            var offsets = RawData.GetOffsets(typeParams[0], typeParams[1]);
            offsets = (offsets.Item1 + (uint)Unsafe.SizeOf<nint>(), offsets.Item2 + (uint)Unsafe.SizeOf<nint>());
            var deserializer0 = CreateCachedDeserializerForType(typeParams[0]);
            var deserializer1 = CreateCachedDeserializerForType(typeParams[1]);
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetTuple(out var tupleBon) && tupleBon.Items >= 2)
                {
                    var res = RuntimeHelpers.GetUninitializedObject(type);
                    tupleBon.TryGet(0, out var itemBon);
                    BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref itemBon };
                    if (deserializer0(ref AsCtx(ref subCtx), ref RawData.Ref(res, offsets.Item1)))
                    {
                        tupleBon.TryGet(1, out itemBon);
                        if (deserializer1(ref AsCtx(ref subCtx), ref RawData.Ref(res, offsets.Item2)))
                        {
                            Unsafe.As<byte, object>(ref value) = res;
                            return true;
                        }
                    }
                }

                return false;
            };
        }

        if (type.IsArray)
        {
            if (!type.IsSZArray) throw new InvalidOperationException("Only SZArray is supported");
            var elementType = type.GetElementType()!;
            var elementTypeDeserializer = CreateCachedDeserializerForType(elementType);
            ref readonly var mt = ref RawData.MethodTableOf(type);
            var offset = mt.BaseSize - (uint)Unsafe.SizeOf<nint>();
            var offsetDelta = mt.ComponentSize;
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetNull())
                {
                    Unsafe.As<byte, object>(ref value) = null;
                    return true;
                }

                if (AsCtx(ref ctx).Bon.TryGetArray(out var arrayBon))
                {
                    var count = arrayBon.Items;
                    var res = Array.CreateInstance(elementType, count);
                    var o = offset;
                    for (var idx = 0u; idx < count; idx++)
                    {
                        arrayBon.TryGet(idx, out var itemBon);
                        BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref itemBon };
                        elementTypeDeserializer(ref AsCtx(ref subCtx), ref RawData.Ref(res, o));
                        o += offsetDelta;
                    }

                    Unsafe.As<byte, object>(ref value) = res;
                    return true;
                }

                return false;
            };
        }

        if (type.SpecializationOf(typeof(List<>)) is { } listType &&
            ReflectionMetadata.FindCollectionByType(listType) is { } listMetadata)
        {
            var elementType = listMetadata.ElementKeyType;
            var elementTypeDeserializer = CreateCachedDeserializerForType(elementType);
            var elementTypeIsValueType = elementType.IsValueType;
            if (elementTypeIsValueType && (*(RawData.MethodTable*)elementType.TypeHandle.Value).ContainsGCPointers)
                throw new InvalidOperationException("Value type with GC pointers is not supported.");
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetNull())
                {
                    Unsafe.As<byte, object>(ref value) = null;
                    return true;
                }

                if (AsCtx(ref ctx).Bon.TryGetArray(out var arrayBon))
                {
                    var count = arrayBon.Items;
                    var res = listMetadata.Creator(count);
                    for (var i = 0u; i < count; i++)
                    {
                        arrayBon.TryGet(i, out var itemBon);
                        BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref itemBon };
                        if (elementTypeIsValueType)
                        {
                            UInt128 temp = default;
                            elementTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<UInt128, byte>(ref temp));
                            listMetadata.Adder(res, ref Unsafe.As<UInt128, byte>(ref temp));
                        }
                        else
                        {
                            object? tempObject = null;
                            elementTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<object, byte>(ref tempObject));
                            listMetadata.Adder(res, ref Unsafe.As<object, byte>(ref tempObject));
                        }
                    }

                    Unsafe.As<byte, object>(ref value) = res;
                    return true;
                }

                return false;
            };
        }

        if (type.SpecializationOf(typeof(HashSet<>)) is { } hashType &&
            ReflectionMetadata.FindCollectionByType(hashType) is { } hashMetadata)
        {
            var elementType = hashMetadata.ElementKeyType;
            var elementTypeDeserializer = CreateCachedDeserializerForType(elementType);
            var elementTypeIsValueType = elementType.IsValueType;
            if (elementTypeIsValueType && (*(RawData.MethodTable*)elementType.TypeHandle.Value).ContainsGCPointers)
                throw new InvalidOperationException("Value type with GC pointers is not supported.");
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetNull())
                {
                    Unsafe.As<byte, object>(ref value) = null;
                    return true;
                }

                if (AsCtx(ref ctx).Bon.TryGetArray(out var arrayBon))
                {
                    var count = arrayBon.Items;
                    var res = hashMetadata.Creator(count);
                    for (var i = 0u; i < count; i++)
                    {
                        arrayBon.TryGet(i, out var itemBon);
                        BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref itemBon };
                        if (elementTypeIsValueType)
                        {
                            UInt128 temp = default;
                            elementTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<UInt128, byte>(ref temp));
                            hashMetadata.Adder(res, ref Unsafe.As<UInt128, byte>(ref temp));
                        }
                        else
                        {
                            object? tempObject = null;
                            elementTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<object, byte>(ref tempObject));
                            hashMetadata.Adder(res, ref Unsafe.As<object, byte>(ref tempObject));
                        }
                    }

                    Unsafe.As<byte, object>(ref value) = res;
                    return true;
                }

                return false;
            };
        }

        if (type.SpecializationOf(typeof(Dictionary<,>)) is { } dictType &&
            ReflectionMetadata.FindCollectionByType(dictType) is { } dictMetadata)
        {
            var keyType = dictMetadata.ElementKeyType;
            var valueType = dictMetadata.ElementValueType;
            var keyTypeDeserializer = CreateCachedDeserializerForType(keyType);
            var valueTypeDeserializer = CreateCachedDeserializerForType(valueType!);
            var keyTypeIsValueType = keyType.IsValueType;
            if (keyTypeIsValueType && (*(RawData.MethodTable*)keyType.TypeHandle.Value).ContainsGCPointers)
                throw new InvalidOperationException("Value type with GC pointers is not supported.");
            var valueTypeIsValueType = valueType!.IsValueType;
            if (valueTypeIsValueType && (*(RawData.MethodTable*)valueType.TypeHandle.Value).ContainsGCPointers)
                throw new InvalidOperationException("Value type with GC pointers is not supported.");
            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (AsCtx(ref ctx).Bon.TryGetNull())
                {
                    Unsafe.As<byte, object>(ref value) = null;
                    return true;
                }

                if (AsCtx(ref ctx).Bon.TryGetDictionary(out var dictBon))
                {
                    var res = dictMetadata.Creator(dictBon.Items / 2);
                    BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref dictBon };
                    while (dictBon.Items > 0)
                    {
                        if (keyTypeIsValueType)
                        {
                            if (valueTypeIsValueType)
                            {
                                UInt128 keyTemp = default;
                                UInt128 valueTemp = default;
                                keyTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<UInt128, byte>(ref keyTemp));
                                valueTypeDeserializer(ref AsCtx(ref subCtx),
                                    ref Unsafe.As<UInt128, byte>(ref valueTemp));
                                dictMetadata.AdderKeyValue(res, ref Unsafe.As<UInt128, byte>(ref keyTemp),
                                    ref Unsafe.As<UInt128, byte>(ref valueTemp));
                            }
                            else
                            {
                                UInt128 keyTemp = default;
                                object? valueTemp = null;
                                keyTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<UInt128, byte>(ref keyTemp));
                                valueTypeDeserializer(ref AsCtx(ref subCtx),
                                    ref Unsafe.As<object, byte>(ref valueTemp));
                                dictMetadata.AdderKeyValue(res, ref Unsafe.As<UInt128, byte>(ref keyTemp),
                                    ref Unsafe.As<object, byte>(ref valueTemp));
                            }
                        }
                        else
                        {
                            if (valueTypeIsValueType)
                            {
                                object? keyTemp = null;
                                UInt128 valueTemp = default;
                                keyTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<object, byte>(ref keyTemp));
                                valueTypeDeserializer(ref AsCtx(ref subCtx),
                                    ref Unsafe.As<UInt128, byte>(ref valueTemp));
                                dictMetadata.AdderKeyValue(res, ref Unsafe.As<object, byte>(ref keyTemp),
                                    ref Unsafe.As<UInt128, byte>(ref valueTemp));
                            }
                            else
                            {
                                object? keyTemp = null;
                                object? valueTemp = null;
                                keyTypeDeserializer(ref AsCtx(ref subCtx), ref Unsafe.As<object, byte>(ref keyTemp));
                                valueTypeDeserializer(ref AsCtx(ref subCtx),
                                    ref Unsafe.As<object, byte>(ref valueTemp));
                                dictMetadata.AdderKeyValue(res, ref Unsafe.As<object, byte>(ref keyTemp),
                                    ref Unsafe.As<object, byte>(ref valueTemp));
                            }
                        }
                    }

                    Unsafe.As<byte, object>(ref value) = res;
                    return true;
                }

                return false;
            };
        }

        var classMetadata = ReflectionMetadata.FindByType(type);
        if (classMetadata != null)
        {
            var persistName = classMetadata.TruePersistedName;
            var persistNameUtf8 = Encoding.UTF8.GetBytes(persistName);
            var fieldDeserializers = new Deserialize[classMetadata.Fields.Length];
            var name2Idx = new SpanByteNoRemoveDictionary<uint>();
            for (var i = 0; i < classMetadata.Fields.Length; i++)
            {
                var field = classMetadata.Fields[i];
                var nameUtf8 = Encoding.UTF8.GetBytes(field.Name);
                name2Idx[nameUtf8] = (uint)i;
                var deserializer = CreateCachedDeserializerForType(field.Type);
                if (field.PropRefSetter != null)
                {
                    var setter = field.PropRefSetter;
                    if (field.Type.IsValueType)
                    {
                        if ((*(RawData.MethodTable*)field.Type.TypeHandle.Value).ContainsGCPointers)
                            throw new InvalidOperationException("Value type with GC pointers is not supported.");
                        fieldDeserializers[i] = (ref DeserializerCtx ctx, ref byte value) =>
                        {
                            UInt128 temp = default;
                            if (deserializer(ref ctx, ref Unsafe.As<UInt128, byte>(ref temp)))
                            {
                                setter(Unsafe.As<byte, object>(ref value), ref Unsafe.As<UInt128, byte>(ref temp));
                                return true;
                            }
                            else
                            {
                                AsCtx(ref ctx).Bon.Skip();
                                return false;
                            }
                        };
                    }
                    else
                    {
                        fieldDeserializers[i] = (ref DeserializerCtx ctx, ref byte value) =>
                        {
                            object? tempObject = null;
                            if (deserializer(ref ctx, ref Unsafe.As<object, byte>(ref tempObject)))
                            {
                                setter(Unsafe.As<byte, object>(ref value), ref Unsafe.As<object, byte>(ref tempObject));
                                return true;
                            }
                            else
                            {
                                AsCtx(ref ctx).Bon.Skip();
                                return false;
                            }
                        };
                    }
                }
                else
                {
                    var offset = field.ByteOffset!.Value;
                    fieldDeserializers[i] = (ref DeserializerCtx ctx, ref byte value) =>
                    {
                        if (deserializer(ref ctx, ref RawData.Ref(Unsafe.As<byte, object>(ref value), offset)))
                        {
                            return true;
                        }
                        else
                        {
                            AsCtx(ref ctx).Bon.Skip();
                            return false;
                        }
                    };
                }
            }

            return (ref DeserializerCtx ctx, ref byte value) =>
            {
                if (!AsCtx(ref ctx).Bon.TryGetClass(out var keyedBon, out var name) ||
                    name.Length != persistNameUtf8.Length || !name.SequenceEqual(persistNameUtf8))
                {
                    AsCtx(ref ctx).Bon.Skip();
                    return false;
                }

                var res = classMetadata.Creator();
                var valuesBon = keyedBon.Values();
                BonDeserializerCtx subCtx = new() { Factory = AsCtx(ref ctx).Factory, Bon = ref valuesBon };
                while (!valuesBon.Eof)
                {
                    if (!name2Idx.TryGetValue(keyedBon.NextKeyUtf8(), out var idx) ||
                        !fieldDeserializers[idx](ref AsCtx(ref subCtx), ref Unsafe.As<object, byte>(ref res)))
                    {
                        valuesBon.Skip();
                    }
                }

                Unsafe.As<byte, object>(ref value) = res;
                return true;
            };
        }

        throw new NotSupportedException("BonDeserialization of " + type.ToSimpleName() + " is not supported.");
    }

    public static BonSerializerFactory Instance { get; } = new();

    public static void Serialize(ref BonBuilder builder, object? value)
    {
        var ctx = new BonSerializerCtx { Factory = Instance, Builder = ref builder };
        Instance.SerializeObject(ref AsCtx(ref ctx), value);
    }

    public static object? Deserialize(ref Bon.Bon bon)
    {
        var ctx = new BonDeserializerCtx { Factory = Instance, Bon = ref bon };
        return Instance.DeserializeObject(ref AsCtx(ref ctx));
    }
}
