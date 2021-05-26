/*******************************************************
*                   Internal Use Only                  *
* Do not forward or distribute outside of Intel/SSG/WOS*
*    Assume AVX2 is supported on the testing machine   *          
*******************************************************/
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace demos
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello Intrinsic!");

            // test matrix multiplication
            for (int i = 8; i <= 32; i += 8)
            {
                // two source matrix
                float[] matrixA = MatrixMultiplication.RandomMatrix(i);
                float[] matrixB = MatrixMultiplication.RandomMatrix(i);

                // matrix for storing return value
                float[] matrixC = MatrixMultiplication.RandomMatrix(i);
                float[] matrixD = MatrixMultiplication.RandomMatrix(i);
                float[] matrixE = MatrixMultiplication.RandomMatrix(i);

                // start test
                double time1 = MatrixMultiplication.benchSoftwareMatrixMultiplication(matrixA, matrixB, ref matrixC, i);
                double time2 = MatrixMultiplication.benchVectorTMatrixMultiplication(matrixA, matrixB, ref matrixD, i);
                double time3 = MatrixMultiplication.benchIntrinsicMatrixMultiplication(matrixA, matrixB, ref matrixE, i);
                // Console.WriteLine("Matrix Multiplication with dimension " + i.ToString() + "x" + i.ToString() + " software v.s intrinsic: " + time1.ToString() + " v.s " + time2.ToString());
                Console.WriteLine(time1.ToString() + " v.s " + time2.ToString() + " v.s " + time3.ToString());

                // MatrixMultiplication.PrintMatrix(matrixC, i);
                // MatrixMultiplication.PrintMatrix(matrixD, i);
                // MatrixMultiplication.PrintMatrix(matrixE, i);
                MatrixMultiplication.VerifyMatrix(matrixC, matrixD, i);
                MatrixMultiplication.VerifyMatrix(matrixC, matrixE, i);
                // MatrixMultiplication.VerifyMatrixIdentical(matrixD, matrixE, i);
            }

            // test matrix vector multiplication
            for (int i = 8; i <= 64; i += 8)
            {
                // source matrix and source vector
                float[] matrixA = MatrixVectorMultiplication.RandomMatrix(i);
                float[] vectorA = MatrixVectorMultiplication.RandomVector(i);

                // vector for storing return value
                float[] vectorB = MatrixVectorMultiplication.RandomVector(i);
                float[] vectorC = MatrixVectorMultiplication.RandomVector(i);
                float[] vectorD = MatrixVectorMultiplication.RandomVector(i);

                double time1 = MatrixVectorMultiplication.benchSoftwareMatrixVectorMultiplication(matrixA, vectorA, ref vectorB, i);
                double time2 = MatrixVectorMultiplication.benchVectorTMatrixVectorMultiplication(matrixA, vectorA, ref vectorC, i);
                double time3 = MatrixVectorMultiplication.benchIntrinsicMatrixVectorMultiplication(matrixA, vectorA, ref vectorD, i);
                // Console.WriteLine("Matrix Vector Multiplication with dimension " + i.ToString() + "x" + i.ToString() + " software v.s intrinsic: " + time1.ToString() + " v.s " + time2.ToString());
                Console.WriteLine(time1.ToString() + " v.s " + time2.ToString() + " v.s " + time3.ToString());

                // MatrixVectorMultiplication.PrintVector(vectorB);
                // MatrixVectorMultiplication.PrintVector(vectorC);
                // MatrixVectorMultiplication.PrintVector(vectorD);
                MatrixVectorMultiplication.VerifyVector(vectorB, vectorC);
                MatrixVectorMultiplication.VerifyVector(vectorB, vectorD);
                // MatrixVectorMultiplication.VerifyVectorIdentical(vectorC, vectorD);
            }
        }
    }

    class MatrixMultiplication
    {
        private static int iteration = 1000000;
        private static Random rnd = new Random();

        // generate a matrix with size n * n
        public static float[] RandomMatrix(int n)
        {
            if (n % 8 != 0)
            {
                Console.WriteLine("Please provide a number which equals 8 * i");
            }
            float[] matrixA = new float[n * n];
            for (int i = 0; i < n * n; i++)
            {
                matrixA[i] = (float)rnd.NextDouble();
            }
            return matrixA;
        }

        // matrixC = matrixA * matrixB
        public static double benchSoftwareMatrixMultiplication(float[] matrixA, float[] matrixB, ref float[] matrixC, int n)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            for (long i = 0; i < iteration; i++)
            {
                MatrixMultiplicationScalar(matrixA, matrixB, ref matrixC, n, n, n, n);
            }
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
            return elapsedTime;
        }

        public static double benchVectorTMatrixMultiplication(float[] matrixA, float[] matrixB, ref float[] matrixC, int n)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            for (long i = 0; i < iteration; i++)
            {
                MatrixMultiplicationVectorT(matrixA, matrixB, ref matrixC, n, n, n, n);
            }
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
            return elapsedTime;
        }

        public static double benchIntrinsicMatrixMultiplication(float[] matrixA, float[] matrixB, ref float[] matrixC, int n)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            for (long i = 0; i < iteration; i++)
            {
                MatrixMultiplicationAVX2(matrixA, matrixB, ref matrixC, n, n, n, n);
            }
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
            return elapsedTime;
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

        static unsafe void MatrixMultiplicationVectorT(float[] matrixA, float[] matrixB, ref float[] matrixC, int matrixARows, int matrixAColumns, int matrixBRows, int matrixBColumns)
        {
            for (int i = 0; i < matrixARows; i += 8)
            {
                for (int j = 0; j < matrixBColumns; j += 8)
                {
                    Vector<float> matrix8x8Row1 = Vector<float>.Zero;
                    Vector<float> matrix8x8Row2 = Vector<float>.Zero;
                    Vector<float> matrix8x8Row3 = Vector<float>.Zero;
                    Vector<float> matrix8x8Row4 = Vector<float>.Zero;
                    Vector<float> matrix8x8Row5 = Vector<float>.Zero;
                    Vector<float> matrix8x8Row6 = Vector<float>.Zero;
                    Vector<float> matrix8x8Row7 = Vector<float>.Zero;
                    Vector<float> matrix8x8Row8 = Vector<float>.Zero;

                    for (int k = 0; k < matrixBRows; k += 8)
                    {
                        // Load eight rows from matrixB to get a 8 * 8 matrix from matrixB
                        Vector<float> vector0 = new Vector<float>(matrixB, (k + 0) * matrixBColumns + j);
                        Vector<float> vector1 = new Vector<float>(matrixB, (k + 1) * matrixBColumns + j);
                        Vector<float> vector2 = new Vector<float>(matrixB, (k + 2) * matrixBColumns + j);
                        Vector<float> vector3 = new Vector<float>(matrixB, (k + 3) * matrixBColumns + j);
                        Vector<float> vector4 = new Vector<float>(matrixB, (k + 4) * matrixBColumns + j);
                        Vector<float> vector5 = new Vector<float>(matrixB, (k + 5) * matrixBColumns + j);
                        Vector<float> vector6 = new Vector<float>(matrixB, (k + 6) * matrixBColumns + j);
                        Vector<float> vector7 = new Vector<float>(matrixB, (k + 7) * matrixBColumns + j);

                        // Broadcast each elements from matrixA
                        Vector<float> vector8 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 0]);
                        Vector<float> vector9 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 1]);
                        Vector<float> vector10 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 2]);
                        Vector<float> vector11 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 3]);
                        Vector<float> vector12 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 4]);
                        Vector<float> vector13 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 5]);
                        Vector<float> vector14 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 6]);
                        Vector<float> vector15 = new Vector<float>(matrixA[(i + 0) * matrixAColumns + k + 7]);

                        // Multiply
                        // row 1, 2
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        // row 3, 4
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        // row 5, 6
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        // row 7, 8
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        // sum
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        // Save current result
                        matrix8x8Row1 = matrix8x8Row1 + vector8;

                        // iterate it for another 7 times
                        vector8 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 0]);
                        vector9 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 1]);
                        vector10 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 2]);
                        vector11 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 3]);
                        vector12 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 4]);
                        vector13 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 5]);
                        vector14 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 6]);
                        vector15 = new Vector<float>(matrixA[(i + 1) * matrixAColumns + k + 7]);
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        matrix8x8Row2 = matrix8x8Row2 + vector8;

                        vector8 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 0]);
                        vector9 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 1]);
                        vector10 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 2]);
                        vector11 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 3]);
                        vector12 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 4]);
                        vector13 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 5]);
                        vector14 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 6]);
                        vector15 = new Vector<float>(matrixA[(i + 2) * matrixAColumns + k + 7]);
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        matrix8x8Row3 = matrix8x8Row3 + vector8;

                        vector8 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 0]);
                        vector9 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 1]);
                        vector10 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 2]);
                        vector11 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 3]);
                        vector12 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 4]);
                        vector13 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 5]);
                        vector14 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 6]);
                        vector15 = new Vector<float>(matrixA[(i + 3) * matrixAColumns + k + 7]);
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        matrix8x8Row4 = matrix8x8Row4 + vector8;

                        vector8 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 0]);
                        vector9 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 1]);
                        vector10 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 2]);
                        vector11 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 3]);
                        vector12 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 4]);
                        vector13 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 5]);
                        vector14 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 6]);
                        vector15 = new Vector<float>(matrixA[(i + 4) * matrixAColumns + k + 7]);
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        matrix8x8Row5 = matrix8x8Row5 + vector8;

                        vector8 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 0]);
                        vector9 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 1]);
                        vector10 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 2]);
                        vector11 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 3]);
                        vector12 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 4]);
                        vector13 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 5]);
                        vector14 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 6]);
                        vector15 = new Vector<float>(matrixA[(i + 5) * matrixAColumns + k + 7]);
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        matrix8x8Row6 = matrix8x8Row6 + vector8;

                        vector8 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 0]);
                        vector9 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 1]);
                        vector10 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 2]);
                        vector11 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 3]);
                        vector12 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 4]);
                        vector13 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 5]);
                        vector14 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 6]);
                        vector15 = new Vector<float>(matrixA[(i + 6) * matrixAColumns + k + 7]);
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        matrix8x8Row7 = matrix8x8Row7 + vector8;

                        vector8 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 0]);
                        vector9 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 1]);
                        vector10 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 2]);
                        vector11 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 3]);
                        vector12 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 4]);
                        vector13 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 5]);
                        vector14 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 6]);
                        vector15 = new Vector<float>(matrixA[(i + 7) * matrixAColumns + k + 7]);
                        vector8 = vector8 * vector0;
                        vector9 = vector9 * vector1;
                        vector8 = vector8 + vector9;
                        vector10 = vector10 * vector2;
                        vector11 = vector11 * vector3;
                        vector10 = vector10 + vector11;
                        vector12 = vector12 * vector4;
                        vector13 = vector13 * vector5;
                        vector12 = vector12 + vector13;
                        vector14 = vector14 * vector6;
                        vector15 = vector15 * vector7;
                        vector14 = vector14 + vector15;
                        vector8 = vector8 + vector10;
                        vector12 = vector12 + vector14;
                        vector8 = vector8 + vector12;
                        matrix8x8Row8 = matrix8x8Row8 + vector8;
                    }

                    matrix8x8Row1.CopyTo(matrixC, (i + 0) * matrixBColumns + j);
                    matrix8x8Row2.CopyTo(matrixC, (i + 1) * matrixBColumns + j);
                    matrix8x8Row3.CopyTo(matrixC, (i + 2) * matrixBColumns + j);
                    matrix8x8Row4.CopyTo(matrixC, (i + 3) * matrixBColumns + j);
                    matrix8x8Row5.CopyTo(matrixC, (i + 4) * matrixBColumns + j);
                    matrix8x8Row6.CopyTo(matrixC, (i + 5) * matrixBColumns + j);
                    matrix8x8Row7.CopyTo(matrixC, (i + 6) * matrixBColumns + j);
                    matrix8x8Row8.CopyTo(matrixC, (i + 7) * matrixBColumns + j);
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
                                var vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 0);
                                var vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 1);
                                var vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 2);
                                var vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 3);
                                var vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 4);
                                var vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 5);
                                var vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 6);
                                var vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 0) * matrixAColumns + k + 7);

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
                                vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 0);
                                vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 1);
                                vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 2);
                                vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 3);
                                vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 4);
                                vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 5);
                                vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 6);
                                vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 1) * matrixAColumns + k + 7);
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

                                vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 0);
                                vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 1);
                                vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 2);
                                vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 3);
                                vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 4);
                                vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 5);
                                vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 6);
                                vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 2) * matrixAColumns + k + 7);
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

                                vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 0);
                                vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 1);
                                vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 2);
                                vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 3);
                                vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 4);
                                vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 5);
                                vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 6);
                                vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 3) * matrixAColumns + k + 7);
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

                                vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 0);
                                vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 1);
                                vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 2);
                                vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 3);
                                vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 4);
                                vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 5);
                                vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 6);
                                vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 4) * matrixAColumns + k + 7);
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

                                vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 0);
                                vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 1);
                                vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 2);
                                vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 3);
                                vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 4);
                                vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 5);
                                vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 6);
                                vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 5) * matrixAColumns + k + 7);
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

                                vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 0);
                                vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 1);
                                vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 2);
                                vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 3);
                                vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 4);
                                vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 5);
                                vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 6);
                                vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 6) * matrixAColumns + k + 7);
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

                                vector8 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 0);
                                vector9 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 1);
                                vector10 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 2);
                                vector11 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 3);
                                vector12 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 4);
                                vector13 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 5);
                                vector14 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 6);
                                vector15 = Avx.BroadcastScalarToVector256(matrixAPtr + (i + 7) * matrixAColumns + k + 7);
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

        static public void PrintMatrix(float[] matrix, int n)
        {
            System.Console.WriteLine("\n***** Matrix starts *****");
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    System.Console.Write(matrix[i * n + j].ToString("F2") + " ");
                }
                System.Console.WriteLine("");
            }
            System.Console.WriteLine("***** Matrix ends  ******\n");
        }

        static public void VerifyMatrix(float[] matrix1, float[] matrix2, int n)
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float delta = matrix1[i * n + j] - matrix2[i * n + j];
                    if (delta > 0.0001 || delta < -0.0001)
                    {
                        System.Console.WriteLine(i.ToString() + "th row " + j.ToString() + "th column is different: matrix1 " + matrix1[i * n + j].ToString() + " matrix2 " + matrix2[i * n + j].ToString());
                    }
                }
            }
        }

        static public void VerifyMatrixIdentical(float[] matrix1, float[] matrix2, int n)
        {
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (matrix1[i * n + j] != matrix2[i * n + j])
                    {
                        System.Console.WriteLine(i.ToString() + "th row " + j.ToString() + "th column is different: matrix1 " + matrix1[i * n + j].ToString() + " matrix2 " + matrix2[i * n + j].ToString());
                    }
                }
            }
        }
    }

    class MatrixVectorMultiplication
    {
        private static int iteration = 1000000;
        private static Random rnd = new Random();

        // generate a matrix with size n * n
        public static float[] RandomMatrix(int n)
        {
            if (n % 8 != 0)
            {
                Console.WriteLine("Please provide a number which equals 8 * i");
            }
            float[] matrixA = new float[n * n];
            for (int i = 0; i < n * n; i++)
            {
                matrixA[i] = (float)rnd.NextDouble();
            }
            return matrixA;
        }

        // generate a vector with size n
        public static float[] RandomVector(int n)
        {
            if (n % 8 != 0)
            {
                Console.WriteLine("Please provide a number which equals 8 * i");
            }
            float[] vectorA = new float[n];
            for (int i = 0; i < n; i++)
            {
                vectorA[i] = (float)rnd.NextDouble();
            }
            return vectorA;
        }

        // ret = mat * vec
        public static double benchSoftwareMatrixVectorMultiplication(float[] mat, float[] vec, ref float[] ret, int n)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            for (long i = 0; i < iteration; i++)
            {
                MatrixVectorMultiplicationScalar(mat, vec, ref ret, n);
            }
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
            return elapsedTime;
        }

        public static double benchVectorTMatrixVectorMultiplication(float[] mat, float[] vec, ref float[] ret, int n)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            for (long i = 0; i < iteration; i++)
            {
                MatrixVectorMultiplicationVectorT(mat, vec, ref ret, n);
            }
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
            return elapsedTime;
        }

        public static double benchIntrinsicMatrixVectorMultiplication(float[] mat, float[] vec, ref float[] ret, int n)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            for (long i = 0; i < iteration; i++)
            {
                MatrixVectorMultiplicationAVX2(mat, vec, ref ret, n);
            }
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            double elapsedTime = ts.Minutes * 60.0 + ts.Seconds + ts.Milliseconds / 1000.0;
            return elapsedTime;
        }

        // mat's dimension is n * n
        // vec's dimension is n
        // ret's dimension is n
        static public unsafe void MatrixVectorMultiplicationScalar(float[] mat, float[] vec, ref float[] ret, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float sum = 0;
                for (int j = 0; j < n; j++)
                {
                    sum += mat[i * n + j] * vec[j];
                }
                ret[i] = sum;
            }
        }

        // mat's dimension is n * n
        // vec's dimension is n
        // ret's dimension is n
        static public unsafe void MatrixVectorMultiplicationVectorT(float[] mat, float[] vec, ref float[] ret, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Vector<float> tmpVector256 = Vector<float>.Zero;
                for (int j = 0; j < n; j += 8)
                {
                    Vector<float> operand1 = new Vector<float>(mat, i * n + j);
                    Vector<float> operand2 = new Vector<float>(vec, j);
                    tmpVector256 += operand1 * operand2;
                }
                float tmp = tmpVector256[0] + tmpVector256[1] + tmpVector256[2] + tmpVector256[3] + tmpVector256[4] + tmpVector256[5] + tmpVector256[6] + tmpVector256[7];
                ret[i] = tmp;
            }
        }

        // mat's dimension is n * n
        // vec's dimension is n
        // ret's dimension is n
        static public unsafe void MatrixVectorMultiplicationAVX2(float[] mat, float[] vec, ref float[] ret, int n)
        {
            float[] tmpVector = new float[8];
            fixed (float* tmpVectorPtr = tmpVector)
            {
                for (int i = 0; i < n; i++)
                {
                    var tmpVector256 = Avx.SetZeroVector256<float>();
                    for (int j = 0; j < n; j += 8)
                    {
                        fixed (float* operand1Ptr = &mat[i * n + j])
                        {
                            fixed (float* operand2Ptr = &vec[j])
                            {
                                var operand1 = Avx.LoadVector256(operand1Ptr);
                                var operand2 = Avx.LoadVector256(operand2Ptr);
                                tmpVector256 = Avx.Add(Avx.Multiply(operand1, operand2), tmpVector256);
                            }
                        }
                    }
                    // we can use following code intead of seven add instructions

                    // version 1
                    // var ymm = _mm256_permute2f128_ps(tmpVector256 , tmpVector256 , 1);
                    // tmpVector256 = Avx.Add(tmpVector256, ymm);
                    // tmpVector256 = Avx.HorizontalAdd(tmpVector256, tmpVector256);
                    // tmpVector256 = Avx.HorizontalAdd(tmpVector256, tmpVector256);
                    // var tmpVector128 = Avx.ExtractVector128(tmpVector256, 0);
                    // ret[i] = Sse.ConvertToSingle(tmpVector128);

                    // version 2
                    // var hi128 = Avx.ExtractVector128(tmpVector256, 1);
                    // var lo128 = Avx.ExtractVector128(tmpVector256, 0);
                    // var sum2lo128 = Sse.Add(hi128, lo128);
                    // var sum2hi128 = Sse.MoveHighToLow(sum2lo128, sum2lo128);
                    // var sum4lo128 = Sse.Add(sum2lo128, sum2hi128);
                    // var sum4hi128 = Sse.Shuffle(sum4lo128, sum4lo128, 1);
                    // var sum8 = Sse.Add(sum4lo128, sum4hi128);
                    // ret[i] = Sse.ConvertToSingle(sum8);

                    Avx.Store(tmpVectorPtr, tmpVector256);
                    float tmp = tmpVector[0] + tmpVector[1] + tmpVector[2] + tmpVector[3] + tmpVector[4] + tmpVector[5] + tmpVector[6] + tmpVector[7];
                    ret[i] = tmp;
                }
            }
        }

        static public void PrintMatrix(float[] mat, int n)
        {
            System.Console.WriteLine("***** Matrix *****");
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    System.Console.Write(mat[i * n + j].ToString("F2") + " ");
                }
                System.Console.WriteLine("");
            }
        }

        static public void PrintVector(float[] vec)
        {
            System.Console.WriteLine("***** Vector *****");
            for (int i = 0; i < vec.Length; i++)
            {
                System.Console.Write(vec[i].ToString("F2") + " ");
            }
            System.Console.WriteLine("");
        }

        static public void VerifyVector(float[] vec1, float[] vec2)
        {
            if (vec1.Length != vec2.Length)
            {
                Console.WriteLine("Two vectors have different length");
            }

            for (int i = 0; i < vec1.Length; i++)
            {
                float delta = vec1[i] - vec2[i];
                if (delta > 0.0001 || delta < -0.0001)
                {
                    Console.WriteLine(i.ToString() + "th element is different: vector1 " + vec1[i].ToString() + " vector2 " + vec2[i].ToString());
                }
            }
        }

        static public void VerifyVectorIdentical(float[] vec1, float[] vec2)
        {
            if (vec1.Length != vec2.Length)
            {
                Console.WriteLine("Two vectors have different length");
            }

            for (int i = 0; i < vec1.Length; i++)
            {
                if (vec1[i] != vec2[i])
                {
                    Console.WriteLine(i.ToString() + "th element is different: vector1 " + vec1[i].ToString() + " vector2 " + vec2[i].ToString());
                }
            }
        }
    }
}
