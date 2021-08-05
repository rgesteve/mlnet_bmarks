/***************************************
*   Internal Test Only. Do Not Share   *
***************************************/
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MatrixMultiplication
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            int iteration = 1000000;
            // To simplify the demos, the demision of the matrix and array equal 8 * i
            // Assume L1 data cache is 32K bytes and we want to make sure all the data can fit in L1 cache
            // For matrix multiplication, we have two input matrix and one output matrix
            // We use row major order to represent matrix
            int matrixARows = 16;
            int matrixAColumns = 32;
            int matrixBRows = 32;
            int matrixBColumns = 16;

            // matrixA is 16 * 32 * 4 bytes = 2K bytes
            float[] matrixA = new float[matrixARows * matrixAColumns];
            // matrixA is 32 * 16 * 4 bytes = 2K bytes
            float[] matrixB = new float[matrixBRows * matrixBColumns];
            // matrixC = matrixA * matrixB
            // matrixC1 stores the return value of scalar version
            float[] matrixC1 = new float[matrixARows * matrixBColumns];
            // matrixC2 stores the return value of AVX2 version
            float[] matrixC2 = new float[matrixARows * matrixBColumns];

            // Initialize matrixA and matrixB
            Random rnd = new Random();
            for (int i = 0; i < matrixA.Length; i++)
            {
                matrixA[i] = (float)rnd.NextDouble();
            }

            for (int i = 0; i < matrixB.Length; i++)
            {
                matrixB[i] = (float)rnd.NextDouble();
            }

            for (int i = 0; i < matrixC1.Length; i++)
            {
                matrixC1[i] = (float)rnd.NextDouble();
                matrixC2[i] = (float)rnd.NextDouble();
            }

            // Test scalar version of matrix multiplication
            long begin = DateTime.Now.Ticks;
            for (int i = 0; i < iteration; i++)
            {
                MatrixMultiplicationScalar(matrixA, matrixB, ref matrixC1, matrixARows, matrixAColumns, matrixBRows, matrixBColumns);
            }
            long time1 = DateTime.Now.Ticks - begin;
            Console.WriteLine("MatrixMultiplicationScalar takes " + time1.ToString() + " returns matrix");
            PrintMatrix(matrixC1, matrixARows, matrixBColumns);

            // Test AVX2 version of matrix multiplication
            // Assume AVX2 is supported on the running machine
            begin = DateTime.Now.Ticks;
            for (int i = 0; i < iteration; i++)
            {
                MatrixMultiplicationAVX2(matrixA, matrixB, ref matrixC2, matrixARows, matrixAColumns, matrixBRows, matrixBColumns);
            }
            long time2 = DateTime.Now.Ticks - begin;
            Console.WriteLine("MatrixVectorMultiplicationAVX2 takes " + time2.ToString() + " returns matrix");
            PrintMatrix(matrixC2, matrixARows, matrixBColumns);

            VerifyMatrix(matrixC1, matrixC2, matrixARows, matrixBColumns);
        }

        static unsafe void MatrixMultiplicationScalar(float[] matrixA, float[] matrixB, ref float[] matrixC, int matrixARows, int matrixAColumns, int matrixBRows, int matrixBColumns)
        {
            for (int i = 0; i < matrixARows; i++)
            {
                for (int j = 0; j < matrixBColumns; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < matrixBRows; k++)
                    {
                        sum += matrixA[i * matrixAColumns + k] * matrixB[k * matrixBColumns + j];
                    }
                    matrixC[i * matrixBColumns + j] = sum;
                }
            }
        }

        // Multiple C/C++ versions of matrix multiplication are available online github/stackoverflow 
        // MatrixMultiplicationAVX2 takes C version from https://github.com/0140454/matrix-multiplication as a reference
        static unsafe void MatrixMultiplicationAVX2(float[] matrixA, float[] matrixB, ref float[] matrixC, int matrixARows, int matrixAColumns, int matrixBRows, int matrixBColumns)
        {
            for (int i = 0; i < matrixARows; i += 8)
            {
                for (int j = 0; j < matrixBColumns; j += 8)
                {
                    var matrix8x8Row1 = Avx.SetZeroVector256<float>();
                    var matrix8x8Row2 = Avx.SetZeroVector256<float>();
                    var matrix8x8Row3 = Avx.SetZeroVector256<float>();
                    var matrix8x8Row4 = Avx.SetZeroVector256<float>();
                    var matrix8x8Row5 = Avx.SetZeroVector256<float>();
                    var matrix8x8Row6 = Avx.SetZeroVector256<float>();
                    var matrix8x8Row7 = Avx.SetZeroVector256<float>();
                    var matrix8x8Row8 = Avx.SetZeroVector256<float>();

                    for (int k = 0; k < matrixBRows; k += 8)
                    {
                        fixed (float* matrixAPtr = matrixA)
                        {
                            fixed (float* matrixBPtr = matrixB)
                            {
                                // Load eight rows from matrixB to get a 8 * 8 matrix from matrixB
                                var vector0 = Avx.LoadVector256(matrixBPtr + (k + 0) * matrixBColumns + j);
                                var vector1 = Avx.LoadVector256(matrixBPtr + (k + 1) * matrixBColumns + j);
                                var vector2 = Avx.LoadVector256(matrixBPtr + (k + 2) * matrixBColumns + j);
                                var vector3 = Avx.LoadVector256(matrixBPtr + (k + 3) * matrixBColumns + j);
                                var vector4 = Avx.LoadVector256(matrixBPtr + (k + 4) * matrixBColumns + j);
                                var vector5 = Avx.LoadVector256(matrixBPtr + (k + 5) * matrixBColumns + j);
                                var vector6 = Avx.LoadVector256(matrixBPtr + (k + 6) * matrixBColumns + j);
                                var vector7 = Avx.LoadVector256(matrixBPtr + (k + 7) * matrixBColumns + j);

                                // Broadcast each elements from matrixA
                                var vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 0]);
                                var vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 1]);
                                var vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 2]);
                                var vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 3]);
                                var vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 4]);
                                var vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 5]);
                                var vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 6]);
                                var vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 0) * matrixAColumns + k + 7]);

                                // Multiply
                                // row 1, 2
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                // row 3, 4
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                // row 5, 6
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                // row 7, 8
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                // sum
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                // Save current result
                                matrix8x8Row1 = Avx.Add(matrix8x8Row1, vector8);

                                // iterate it for another 7 times
                                vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 0]);
                                vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 1]);
                                vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 2]);
                                vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 3]);
                                vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 4]);
                                vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 5]);
                                vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 6]);
                                vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 1) * matrixAColumns + k + 7]);
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                matrix8x8Row2 = Avx.Add(matrix8x8Row2, vector8);

                                vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 0]);
                                vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 1]);
                                vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 2]);
                                vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 3]);
                                vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 4]);
                                vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 5]);
                                vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 6]);
                                vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 2) * matrixAColumns + k + 7]);
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                matrix8x8Row3 = Avx.Add(matrix8x8Row3, vector8);

                                vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 0]);
                                vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 1]);
                                vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 2]);
                                vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 3]);
                                vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 4]);
                                vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 5]);
                                vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 6]);
                                vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 3) * matrixAColumns + k + 7]);
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                matrix8x8Row4 = Avx.Add(matrix8x8Row4, vector8);

                                vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 0]);
                                vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 1]);
                                vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 2]);
                                vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 3]);
                                vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 4]);
                                vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 5]);
                                vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 6]);
                                vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 4) * matrixAColumns + k + 7]);
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                matrix8x8Row5 = Avx.Add(matrix8x8Row5, vector8);

                                vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 0]);
                                vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 1]);
                                vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 2]);
                                vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 3]);
                                vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 4]);
                                vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 5]);
                                vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 6]);
                                vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 5) * matrixAColumns + k + 7]);
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                matrix8x8Row6 = Avx.Add(matrix8x8Row6, vector8);

                                vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 0]);
                                vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 1]);
                                vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 2]);
                                vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 3]);
                                vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 4]);
                                vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 5]);
                                vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 6]);
                                vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 6) * matrixAColumns + k + 7]);
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                matrix8x8Row7 = Avx.Add(matrix8x8Row7, vector8);

                                vector8 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 0]);
                                vector9 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 1]);
                                vector10 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 2]);
                                vector11 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 3]);
                                vector12 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 4]);
                                vector13 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 5]);
                                vector14 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 6]);
                                vector15 = Avx.BroadcastScalarToVector256(&matrixAPtr[(i + 7) * matrixAColumns + k + 7]);
                                vector8 = Avx.Multiply(vector8, vector0);
                                vector9 = Avx.Multiply(vector9, vector1);
                                vector8 = Avx.Add(vector8, vector9);
                                vector10 = Avx.Multiply(vector10, vector2);
                                vector11 = Avx.Multiply(vector11, vector3);
                                vector10 = Avx.Add(vector10, vector11);
                                vector12 = Avx.Multiply(vector12, vector4);
                                vector13 = Avx.Multiply(vector13, vector5);
                                vector12 = Avx.Add(vector12, vector13);
                                vector14 = Avx.Multiply(vector14, vector6);
                                vector15 = Avx.Multiply(vector15, vector7);
                                vector14 = Avx.Add(vector14, vector15);
                                vector8 = Avx.Add(vector8, vector10);
                                vector12 = Avx.Add(vector12, vector14);
                                vector8 = Avx.Add(vector8, vector12);
                                matrix8x8Row8 = Avx.Add(matrix8x8Row8, vector8);
                            }
                        }
                    }

                    fixed (float* matrixCPtr = matrixC)
                    {
                        Avx.Store((matrixCPtr + (i + 0) * matrixBColumns + j), matrix8x8Row1);
                        Avx.Store((matrixCPtr + (i + 1) * matrixBColumns + j), matrix8x8Row2);
                        Avx.Store((matrixCPtr + (i + 2) * matrixBColumns + j), matrix8x8Row3);
                        Avx.Store((matrixCPtr + (i + 3) * matrixBColumns + j), matrix8x8Row4);
                        Avx.Store((matrixCPtr + (i + 4) * matrixBColumns + j), matrix8x8Row5);
                        Avx.Store((matrixCPtr + (i + 5) * matrixBColumns + j), matrix8x8Row6);
                        Avx.Store((matrixCPtr + (i + 6) * matrixBColumns + j), matrix8x8Row7);
                        Avx.Store((matrixCPtr + (i + 7) * matrixBColumns + j), matrix8x8Row8);
                    }
                }
            }
        }

        static public void PrintMatrix(float[] matrix, int rows, int columns)
        {
            System.Console.WriteLine("\n***** Matrix starts *****");
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    System.Console.Write(matrix[i * rows + j].ToString("F2") + " ");
                }
                System.Console.WriteLine("");
            }
            System.Console.WriteLine("***** Matrix ends  ******\n");
        }

        static public void VerifyMatrix(float[] matrix1, float[] matrix2, int rows, int columns)
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (matrix1[i * rows + j] != matrix2[i * rows + j])
                    {
                        System.Console.WriteLine(i.ToString() + "th row " + j.ToString() + "th column is different: matrix1 " + matrix1[i * rows + j].ToString() + " matrix2 " + matrix2[i * rows + j].ToString());
                    }
                }
            }
        }
    }
}
