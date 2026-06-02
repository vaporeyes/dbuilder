// ABOUTME: Models UDBScript API helper conversions for vectors and universal values.
// ABOUTME: Provides pure conversion targets for future script API wrapper execution.

using System.Collections;
using System.Dynamic;
using System.Numerics;
using DBuilder.Geometry;

namespace DBuilder.IO;

public sealed record UdbScriptVector2DWrapper(double X, double Y);

public sealed record UdbScriptVector3DWrapper(double X, double Y, double Z);

public sealed record UdbScriptUniversalValue(int Type, object? Value);

public sealed class UdbScriptVectorConversionException : Exception
{
    public UdbScriptVectorConversionException(string message)
        : base(message)
    {
    }
}

public static class UdbScriptApiConversionModel
{
    public const string VectorConversionFailureMessage =
        "Data must be a Vector2D, Vector3D, an array of numbers, or an object with (x, y, z) members.";

    public static Vector3D GetVector3DFromObject(object data)
    {
        if (data is Vector2D vector2D)
            return vector2D;

        if (data is UdbScriptVector2DWrapper vector2DWrapper)
            return new Vector2D(vector2DWrapper.X, vector2DWrapper.Y);

        if (data is Vector3D vector3D)
            return vector3D;

        if (data is UdbScriptVector3DWrapper vector3DWrapper)
            return new Vector3D(vector3DWrapper.X, vector3DWrapper.Y, vector3DWrapper.Z);

        if (data.GetType().IsArray)
            return VectorFromArray(data);

        if (data is ExpandoObject expando)
            return VectorFromDictionary((IDictionary<string, object?>)expando);

        if (data is IReadOnlyDictionary<string, object?> dictionary)
            return VectorFromDictionary(dictionary);

        throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);
    }

    public static object? GetConvertedUniversalValue(UdbScriptUniversalValue value)
    {
        return (UniversalType)value.Type switch
        {
            UniversalType.AngleRadians or UniversalType.AngleDegreesFloat or UniversalType.Float => Convert.ToDouble(value.Value),
            UniversalType.AngleDegrees or UniversalType.AngleByte or UniversalType.Color or UniversalType.EnumBits
                or UniversalType.EnumOption or UniversalType.Integer or UniversalType.LinedefTag
                or UniversalType.LinedefType or UniversalType.SectorEffect or UniversalType.SectorTag
                or UniversalType.ThingTag or UniversalType.ThingType => Convert.ToInt32(value.Value),
            UniversalType.Boolean => Convert.ToBoolean(value.Value),
            UniversalType.Flat or UniversalType.String or UniversalType.Texture or UniversalType.EnumStrings
                or UniversalType.ThingClass => Convert.ToString(value.Value),
            _ => null,
        };
    }

    public static Type? GetTypeFromUniversalType(int type)
    {
        return (UniversalType)type switch
        {
            UniversalType.AngleRadians or UniversalType.AngleDegreesFloat or UniversalType.Float => typeof(double),
            UniversalType.AngleDegrees or UniversalType.AngleByte or UniversalType.Color or UniversalType.EnumBits
                or UniversalType.EnumOption or UniversalType.Integer or UniversalType.LinedefTag
                or UniversalType.LinedefType or UniversalType.SectorEffect or UniversalType.SectorTag
                or UniversalType.ThingTag or UniversalType.ThingType => typeof(int),
            UniversalType.Boolean => typeof(bool),
            UniversalType.Flat or UniversalType.String or UniversalType.Texture or UniversalType.EnumStrings
                or UniversalType.ThingClass => typeof(string),
            _ => null,
        };
    }

    private static Vector3D VectorFromArray(object data)
    {
        if (data is not IEnumerable enumerable)
            throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);

        var values = new List<double>();
        foreach (object? raw in enumerable)
        {
            if (raw is double number)
            {
                values.Add(number);
            }
            else if (raw is BigInteger bigInteger)
            {
                values.Add((double)bigInteger);
            }
            else
            {
                throw new UdbScriptVectorConversionException("Values in array must be numbers.");
            }
        }

        return values.Count switch
        {
            2 => new Vector2D(values[0], values[1]),
            3 => new Vector3D(values[0], values[1], values[2]),
            _ => throw new UdbScriptVectorConversionException(VectorConversionFailureMessage),
        };
    }

    private static Vector3D VectorFromDictionary(IReadOnlyDictionary<string, object?> values)
    {
        double x = double.NaN;
        double y = double.NaN;
        double z = 0.0;

        if (values.ContainsKey("x"))
            x = ConvertMember("x", values["x"]);

        if (values.ContainsKey("y"))
            y = ConvertMember("y", values["y"]);

        if (values.ContainsKey("z"))
            z = ConvertMember("z", values["z"]);

        if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsNaN(z))
            return new Vector3D(x, y, z);

        throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);
    }

    private static Vector3D VectorFromDictionary(IDictionary<string, object?> values)
    {
        double x = double.NaN;
        double y = double.NaN;
        double z = 0.0;

        if (values.ContainsKey("x"))
            x = ConvertMember("x", values["x"]);

        if (values.ContainsKey("y"))
            y = ConvertMember("y", values["y"]);

        if (values.ContainsKey("z"))
            z = ConvertMember("z", values["z"]);

        if (!double.IsNaN(x) && !double.IsNaN(y) && !double.IsNaN(z))
            return new Vector3D(x, y, z);

        throw new UdbScriptVectorConversionException(VectorConversionFailureMessage);
    }

    private static double ConvertMember(string name, object? value)
    {
        try
        {
            return Convert.ToDouble(value);
        }
        catch (Exception ex)
        {
            throw new UdbScriptVectorConversionException("Can not convert '" + name + "' property of data: " + ex.Message);
        }
    }
}
