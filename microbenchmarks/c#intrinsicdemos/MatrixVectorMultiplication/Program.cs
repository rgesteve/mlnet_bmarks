/***************************************
*   Internal Test Only. Do Not Share   *
***************************************/
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace MatrixVectorMultiplication
{
    class Program
    {
        static unsafe void Main(string[] args)
        {
            int iteration = 1000000;
            // To simplify the test, the demision of the matrix and array equal 8 * i
            // Assume L1 data cache is 32K bytes and we want to make sure all the data can fit in L1 cache
            // For matrix vector multiplication, we have one input matrix, one input vector and one output vector
            int matrixARows = 16;
            int matrixAColumns = 128;
            int vectorASize = 128;

            // matrixA is 16 * 128 * 4 bytes = 8K bytes
            float[,] matrixA = new float[matrixARows, matrixAColumns];
            // vectorA is 128 * 4 = 0.5K bytes
            float[] vectorA = new float[vectorASize];
            // vectorB = matrixA * vectorA
            // vectorB1 stores the return value of scalar version
            // vectorB2 stores the return value of AVX2 version
            float[] vectorB1 = new float[matrixARows];
            float[] vectorB2 = new float[matrixARows];

            // Initialize matrixA and vectorA
            Random rnd = new Random();
            for (int i = 0; i < matrixA.GetLength(0); i++)
            {
                for (int j = 0; j < matrixA.GetLength(1); j++)
                {
                    matrixA[i, j] = (float)rnd.NextDouble();
                }
            }

            for (int i = 0; i < vectorA.Length; i++)
            {
                vectorA[i] = (float)rnd.NextDouble();
            }

            for (int i = 0; i < vectorB1.Length; i++)
            {
                vectorB1[i] = (float)rnd.NextDouble();
                vectorB2[i] = (float)rnd.NextDouble();
            }

            // PrintMatrix(matrixA);
            // PrintVector(vectorA);

            // Test scalar version of matrix vector multiplication
            long begin = DateTime.Now.Ticks;
            for (int i = 0; i < iteration; i++)
            {
                MatrixVectorMultiplicationScalar(matrixA, vectorA, ref vectorB1);
            }
            long time1 = DateTime.Now.Ticks - begin;
            Console.WriteLine("MatrixVectorMultiplicationScalar takes " + time1.ToString() + " returns vector");
            PrintVector(vectorB1);

            // Test avx version of matrix vector multiplication
            // Assume AVX2 is surpported on the running machine
            begin = DateTime.Now.Ticks;
            for (int i = 0; i < iteration; i++)
            {
                MatrixVectorMultiplicationAVX2(matrixA, vectorA, ref vectorB2);
            }
            long time2 = DateTime.Now.Ticks - begin;
            Console.WriteLine("MatrixVectorMultiplicationAVX2 takes " + time2.ToString() + " returns vector");
            PrintVector(vectorB2);

            VerifyVector(vectorB1, vectorB2);
        }

        // mat's dimension is m * n
        // vec's dimension is n
        // ret's dimension is n
        // With the above assumption, we do not check the dimension explicitly
        static public unsafe void MatrixVectorMultiplicationScalar(float[,] mat, float[] vec, ref float[] ret)
        {
            for (int i = 0; i < mat.GetLength(0); i++)
            {
                float sum = 0;
                for (int j = 0; j < mat.GetLength(1); j++)
                {
                    sum += mat[i, j] * vec[j];
                }
                ret[i] = sum;
            }
        }

        // mat's dimension is m * n
        // vec's dimension is n
        // ret's dimension is n
        // With the above assumption, we do not check the dimension explicitly
        static public unsafe void MatrixVectorMultiplicationAVX2(float[,] mat, float[] vec, ref float[] ret)
        {
            float[] tmpVector = new float[8];
            fixed (float* tmpVectorPtr = tmpVector)
            {
                for (int i = 0; i < mat.GetLength(0); i++)
                {
                    var tmpVector256 = Avx.SetZeroVector256<float>();
                    for (int j = 0; j < mat.GetLength(1); j += 8)
                    {
                        fixed (float* operand1Ptr = &mat[i, j])
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
                    // var ymm = _mm256_permute2f128_ps(tmpVector256 , tmpVector256 , 1);
                    // tmpVector256 = Avx.Add(tmpVector256, ymm);
                    // tmpVector256 = Avx.HorizontalAdd(tmpVector256, tmpVector256);
                    // tmpVector256 = Avx.HorizontalAdd(tmpVector256, tmpVector256);
                    // Avx.Store(tmpVectorPtr, tmpVector256);
                    // ret[i] = tmpVector[0];
                    Avx.Store(tmpVectorPtr, tmpVector256);
                    float tmp = tmpVector[0] + tmpVector[1] + tmpVector[2] + tmpVector[3] + tmpVector[4] + tmpVector[5] + tmpVector[6] + tmpVector[7];
                    ret[i] = tmp;
                }
            }
        }

        static public void PrintMatrix(float[,] mat)
        {
            System.Console.WriteLine("***** Matrix *****");
            for (int i = 0; i < mat.GetLength(0); i++)
            {
                for (int j = 0; j < mat.GetLength(1); j++)
                {
                    System.Console.Write(mat[i, j].ToString("F2") + " ");
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
                if (vec1[i] != vec2[i])
                {
                    Console.WriteLine(i.ToString() + "th element is different: vector1 " + vec1[i].ToString() + " vector2 " + vec2[i].ToString());
                }
            }
        }
    }
}
