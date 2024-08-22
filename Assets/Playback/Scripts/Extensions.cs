using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Globalization;

public static class QuaternionExtentsions
{
    public static Quaternion ExtractRotation(this Matrix4x4 matrix)
    {
        Vector3 forward;
        forward.x = matrix.m02;
        forward.y = matrix.m12;
        forward.z = matrix.m22;

        Vector3 upwards;
        upwards.x = matrix.m01;
        upwards.y = matrix.m11;
        upwards.z = matrix.m21;

        return Quaternion.LookRotation(forward, upwards);
    }
}

public static class VectorExtensions
{
    public static Vector3 ARKitToUnity(this Vector3 vector)
    {
        var newVector = new Vector3();

        newVector.x = vector.x;
        newVector.y = vector.y;
        newVector.z = -vector.z;

        return newVector;
    }

    public static Vector3 ExtractPosition(this Matrix4x4 matrix)
    {
        Vector3 position;
        position.x = matrix.m03;
        position.y = matrix.m13;
        position.z = matrix.m23;
        return position;
    }

    public static Vector3 ExtractScale(this Matrix4x4 matrix)
    {
        Vector3 scale;
        scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
        scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
        scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
        return scale;
    }

    public static Vector4 IntrinsicsFrom3x3String(string matrixString)
    {
        var Matrix = MatrixExtensions.From3x3String(matrixString);

        float fx = Matrix.m00;
        float fy = Matrix.m11;
        float cx = Matrix.m02;
        float cy = Matrix.m12;

        return new Vector4(fx, fy, cx, cy);
    }

    public static Vector2Int FromVector2IntString(string vectorString)
    {
        char[] separators = new char[] { ',', '[', ']' };

        var split = vectorString.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        var intSplit = Array.ConvertAll(split, i => Convert.ToInt32(i));

        return new Vector2Int(intSplit[0], intSplit[1]);
    }

    public static Vector3 FromVector3String(string vectorString)
    {
        char[] separators = new char[] { ',', '[', ']' };

        var split = vectorString.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        var intSplit = Array.ConvertAll(split, i => (float)Convert.ToDouble(i, CultureInfo.InvariantCulture));

        return new Vector3(intSplit[0], intSplit[1], intSplit[2]);
    }

    public static Vector3 FromArray(float[] vector)
    {
        return new Vector3(vector[0], vector[1], vector[2]);
    }

    public static Vector2Int FromArray(List<int> vectorList)
    {
        if (vectorList.Count == 0)
        {
            throw new Exception("Wrong number of elements");
        }

        return new Vector2Int(vectorList[0], vectorList[1]);
    }
}

public static class MatrixExtensions
{
    public static Matrix4x4 ARKitToUnity(this Matrix4x4 matrix)
    {
        var localScale = matrix.ExtractScale();
        var rotation = matrix.ExtractRotation().eulerAngles;
        var position = matrix.ExtractPosition();

        position.z = -position.z;
        rotation.x = -rotation.x;
        rotation.y = -rotation.y;

        return Matrix4x4.Translate(position) * Matrix4x4.Rotate(Quaternion.Euler(rotation)) * Matrix4x4.Scale(localScale);
    }

    public static Matrix4x4 From3x3String(string matrixString)
    {
        char[] separators = new char[] { ',', '[', ']' };

        var split = matrixString.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length != 9)
        {
            throw new Exception("Wrong number of elements");
        }

        // Memory layout:
        //                row no (=vertical)
        //               |  0   1   2   3
        //            ---+----------------
        //            0  | m00 m10 m20 m30
        // column no  1  | m01 m11 m21 m31
        // (=horiz)   2  | m02 m12 m22 m32
        //            3  | m03 m13 m23 m33
        //
        // @ref: https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Math/Matrix4x4.cs

        var floatSplit = Array.ConvertAll(split, i => (float)Convert.ToDouble(i, CultureInfo.InvariantCulture));

        Matrix4x4 Matrix = Matrix4x4.identity;

        Matrix.SetColumn(0, new Vector4(floatSplit[0], floatSplit[1], floatSplit[2], 0.0f));
        Matrix.SetColumn(1, new Vector4(floatSplit[3], floatSplit[4], floatSplit[5], 0.0f));
        Matrix.SetColumn(2, new Vector4(floatSplit[6], floatSplit[7], floatSplit[8], 0.0f));

        return Matrix;
    }

    public static Matrix4x4 From4x4String(string matrixString)
    {
        char[] separators = new char[] { ',', '[', ']' };

        var split = matrixString.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        if (split.Length != 16)
        {
            throw new Exception("Wrong number of elements");
        }

        var floatSplit = Array.ConvertAll(split, i => (float)Convert.ToDouble(i, CultureInfo.InvariantCulture));

        Matrix4x4 Matrix = Matrix4x4.identity;

        Matrix.SetColumn(0, new Vector4(floatSplit[0], floatSplit[1], floatSplit[2], floatSplit[3]));
        Matrix.SetColumn(1, new Vector4(floatSplit[4], floatSplit[5], floatSplit[6], floatSplit[7]));
        Matrix.SetColumn(2, new Vector4(floatSplit[8], floatSplit[9], floatSplit[10], floatSplit[11]));
        Matrix.SetColumn(3, new Vector4(floatSplit[12], floatSplit[13], floatSplit[14], floatSplit[15]));

        return Matrix;
    }

    public static Matrix4x4 FromArray(float[] matrix)
    {
        if (!(matrix.Length == 9 || matrix.Length == 16))
        {
            throw new Exception("Wrong number of elements");
        }

        Matrix4x4 Matrix = Matrix4x4.identity;

        if (matrix.Length == 9)
        {
            Matrix.SetColumn(0, new Vector4(matrix[0], matrix[1], matrix[2], 0.0f));
            Matrix.SetColumn(1, new Vector4(matrix[3], matrix[4], matrix[5], 0.0f));
            Matrix.SetColumn(2, new Vector4(matrix[6], matrix[7], matrix[8], 0.0f));
        }

        if (matrix.Length == 16)
        {
            Matrix.SetColumn(0, new Vector4(matrix[0], matrix[1], matrix[2], matrix[3]));
            Matrix.SetColumn(1, new Vector4(matrix[4], matrix[5], matrix[6], matrix[7]));
            Matrix.SetColumn(2, new Vector4(matrix[8], matrix[9], matrix[10], matrix[11]));
            Matrix.SetColumn(3, new Vector4(matrix[12], matrix[13], matrix[14], matrix[15]));
        }

        return Matrix;
    }
}

public static class TransformExtensions
{
    public static void FromARKitMatrix(this Transform transform, Matrix4x4 matrix)
    {
        var localScale = matrix.ExtractScale();
        var rotation = matrix.ExtractRotation().eulerAngles;
        var position = matrix.ExtractPosition();

        position.z = -position.z;
        rotation.x = -rotation.x;
        rotation.y = -rotation.y;

        transform.localScale = localScale;
        transform.rotation = Quaternion.Euler(rotation);
        transform.position = position;
    }
}

public static class SerializerExtensions
{
    public static IEnumerable<T> DeserializeObjects<T>(string input)
    {
        JsonSerializer serializer = new JsonSerializer();
        using (var strreader = new StringReader(input))
        using (var jsonreader = new JsonTextReader(strreader))
        {
            jsonreader.SupportMultipleContent = true;
            while (jsonreader.Read())
            {
                yield return serializer.Deserialize<T>(jsonreader);
            }

        }
    }
}