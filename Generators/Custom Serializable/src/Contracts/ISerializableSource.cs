using System;

public interface ISerializableSource
{
    ISerializedStruct Serialize(ISerializationResolver resolver = null);
}
