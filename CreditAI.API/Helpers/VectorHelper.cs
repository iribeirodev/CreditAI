using System.Runtime.InteropServices;

namespace CreditAI.API.Helpers;

public class VectorHelper
{
    /// <summary>
    /// Converte de float[] (Vetor da IA) para byte[] (SQL Server)
    /// </summary>
    public static byte[] ToByteArray(float[] floatArray) =>
        MemoryMarshal.AsBytes(floatArray.AsSpan()).ToArray();

    /// <summary>
    /// Converte de  byte[] (SQL Server) para float[] (Vetor da IA) para cálculos no C#
    /// </summary>
    public static float[] ToFloatArray(byte[] byteArray) =>
        MemoryMarshal.Cast<byte, float>(byteArray).ToArray();
}
