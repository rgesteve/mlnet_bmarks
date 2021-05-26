using System.Net;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    long stop0 = DateTime.Now.Ticks;

    kernel.benchFFT();
    long stop1 = DateTime.Now.Ticks;

    kernel.benchSOR();
    long stop2 = DateTime.Now.Ticks;

    kernel.benchMonteCarlo();
    long stop3 = DateTime.Now.Ticks;

    kernel.benchSparseMult();
    long stop4 = DateTime.Now.Ticks;

    kernel.benchmarkLU();
    long stop5 = DateTime.Now.Ticks;


    TimeSpan elapsedSpan1 = new TimeSpan(stop1 - stop0);
    TimeSpan elapsedSpan2 = new TimeSpan(stop2 - stop1);
    TimeSpan elapsedSpan3 = new TimeSpan(stop3 - stop2);
    TimeSpan elapsedSpan4 = new TimeSpan(stop4 - stop3);
    TimeSpan elapsedSpan5 = new TimeSpan(stop5 - stop4);

    TimeSpan elapsedSpan = new TimeSpan(stop5 - stop0);

    return req.CreateResponse(HttpStatusCode.OK, " SciMark runs " + elapsedSpan.TotalMilliseconds + " FFT: " + elapsedSpan1.TotalMilliseconds + " SOR: " + elapsedSpan2.TotalMilliseconds + " Monte Carlo: " + elapsedSpan3.TotalMilliseconds + " Sparse MatMult: " + elapsedSpan4.TotalMilliseconds + " LU: " + elapsedSpan5.TotalMilliseconds);
}

public class Constants
{
    public const double RESOLUTION_DEFAULT = 10.0; /*secs*/
    public const double RESOLUTION_TINY = 1;   /*secs*/
    public const int RANDOM_SEED = 101010;

    // default: small (cache-contained) problem sizes
    //
    public const int FFT_SIZE = 1024; // must be a power of two
    public const int SOR_SIZE = 100; // NxN grid
    public const int SPARSE_SIZE_M = 1000;
    public const int SPARSE_SIZE_nz = 5000;
    public const int LU_SIZE = 100;

    // large (out-of-cache) problem sizes
    //
    public const int LG_FFT_SIZE = 1048576; // must be a power of two
    public const int LG_SOR_SIZE = 1000; // NxN grid
    public const int LG_SPARSE_SIZE_M = 100000;
    public const int LG_SPARSE_SIZE_nz = 1000000;
    public const int LG_LU_SIZE = 1000;

    // tiny problem sizes (used to mainly to preload network classes 
    //                     for applet, so that network download times
    //                     are factored out of benchmark.)
    //
    public const int TINY_FFT_SIZE = 16; // must be a power of two
    public const int TINY_SOR_SIZE = 10; // NxN grid
    public const int TINY_SPARSE_SIZE_M = 10;
    public const int TINY_SPARSE_SIZE_N = 10;
    public const int TINY_SPARSE_SIZE_nz = 50;
    public const int TINY_LU_SIZE = 10;
}

/// <summary>Computes FFT's of complex, double precision data where n is an integer power of 2.
/// This appears to be slower than the Radix2 method,
/// but the code is smaller and simpler, and it requires no extra storage.
/// </P>
/// </summary>
/// 
/// <author> 
/// Bruce R. Miller bruce.miller@nist.gov,
/// Derived from GSL (Gnu Scientific Library), 
/// GSL's FFT Code by Brian Gough bjg@vvv.lanl.gov
/// </author>

public class FFT
{
    public static double num_flops(int N)
    {
        double Nd = (double)N;
        double logN = (double)log2(N);

        return (5.0 * Nd - 2) * logN + 2 * (Nd + 1);
    }


    /// <summary>
    /// Compute Fast Fourier Transform of (complex) data, in place.
    /// </summary>
    public static void transform(double[] data)
    {
        transform_internal(data, -1);
    }

    /// <summary>
    /// Compute Inverse Fast Fourier Transform of (complex) data, in place.
    /// </summary>
    public static void inverse(double[] data)
    {
        transform_internal(data, +1);
        // Normalize
        int nd = data.Length;
        int n = nd / 2;
        double norm = 1 / ((double)n);
        for (int i = 0; i < nd; i++)
            data[i] *= norm;
    }

    /// <summary>
    /// Accuracy check on FFT of data. Make a copy of data, Compute the FFT, then
    /// the inverse and compare to the original.  Returns the rms difference.
    /// </summary>
    public static double test(double[] data)
    {
        int nd = data.Length;
        // Make duplicate for comparison
        double[] copy = new double[nd];
        Array.Copy(data, 0, copy, 0, nd);
        // Transform & invert
        transform(data);
        inverse(data);
        // Compute RMS difference.
        double diff = 0.0;
        for (int i = 0; i < nd; i++)
        {
            double d = data[i] - copy[i];
            diff += d * d;
        }
        return Math.Sqrt(diff / nd);
    }

    /// <summary>
    /// Make a random array of n (complex) elements. 
    /// </summary>
    public static double[] makeRandom(int n)
    {
        int nd = 2 * n;
        double[] data = new double[nd];
        System.Random r = new System.Random();
        for (int i = 0; i < nd; i++)
            data[i] = r.NextDouble();
        return data;
    }

    protected internal static int log2(int n)
    {
        int log = 0;
        for (int k = 1; k < n; k *= 2, log++)
            ;
        if (n != (1 << log))
            throw new Exception("FFT: Data length is not a power of 2!: " + n);
        return log;
    }

    protected internal static void transform_internal(double[] data, int direction)
    {
        if (data.Length == 0)
            return;
        int n = data.Length / 2;
        if (n == 1)
            return;
        // Identity operation!
        int logn = log2(n);

        /* bit reverse the input data for decimation in time algorithm */
        bitreverse(data);

        /* apply fft recursion */
        /* this loop executed log2(N) times */
        for (int bit = 0, dual = 1; bit < logn; bit++, dual *= 2)
        {
            double w_real = 1.0;
            double w_imag = 0.0;

            double theta = 2.0 * direction * Math.PI / (2.0 * (double)dual);
            double s = Math.Sin(theta);
            double t = Math.Sin(theta / 2.0);
            double s2 = 2.0 * t * t;

            /* a = 0 */
            for (int b = 0; b < n; b += 2 * dual)
            {
                int i = 2 * b;
                int j = 2 * (b + dual);

                double wd_real = data[j];
                double wd_imag = data[j + 1];

                data[j] = data[i] - wd_real;
                data[j + 1] = data[i + 1] - wd_imag;
                data[i] += wd_real;
                data[i + 1] += wd_imag;
            }

            /* a = 1 .. (dual-1) */
            for (int a = 1; a < dual; a++)
            {
                /* trignometric recurrence for w-> exp(i theta) w */
                {
                    double tmp_real = w_real - s * w_imag - s2 * w_real;
                    double tmp_imag = w_imag + s * w_real - s2 * w_imag;
                    w_real = tmp_real;
                    w_imag = tmp_imag;
                }
                for (int b = 0; b < n; b += 2 * dual)
                {
                    int i = 2 * (b + a);
                    int j = 2 * (b + a + dual);

                    double z1_real = data[j];
                    double z1_imag = data[j + 1];

                    double wd_real = w_real * z1_real - w_imag * z1_imag;
                    double wd_imag = w_real * z1_imag + w_imag * z1_real;

                    data[j] = data[i] - wd_real;
                    data[j + 1] = data[i + 1] - wd_imag;
                    data[i] += wd_real;
                    data[i + 1] += wd_imag;
                }
            }
        }
    }


    protected internal static void bitreverse(double[] data)
    {
        /* This is the Goldrader bit-reversal algorithm */
        int n = data.Length / 2;
        int nm1 = n - 1;
        int i = 0;
        int j = 0;
        for (; i < nm1; i++)
        {
            //int ii = 2*i;
            int ii = i << 1;

            //int jj = 2*j;
            int jj = j << 1;

            //int k = n / 2 ;
            int k = n >> 1;

            if (i < j)
            {
                double tmp_real = data[ii];
                double tmp_imag = data[ii + 1];
                data[ii] = data[jj];
                data[ii + 1] = data[jj + 1];
                data[jj] = tmp_real;
                data[jj + 1] = tmp_imag;
            }

            while (k <= j)
            {
                //j = j - k ;
                j -= k;

                //k = k / 2 ; 
                k >>= 1;
            }
            j += k;
        }
    }
}


/// <summary>
/// LU matrix factorization. (Based on TNT implementation.)
/// Decomposes a matrix A  into a triangular lower triangular
/// factor (L) and an upper triangular factor (U) such that
/// A = L*U.  By convnetion, the main diagonal of L consists
/// of 1's so that L and U can be stored compactly in
/// a NxN matrix.
/// </summary>
public class LU
{
    private double[][] _LU;
    private int[] _pivot;

    /// <summary>
    /// Returns a <em>copy</em> of the compact LU factorization.
    /// (useful mainly for debugging.)
    /// </summary>
    /// 
    /// <returns>
    /// the compact LU factorization.  The U factor
    /// is stored in the upper triangular portion, and the L
    /// factor is stored in the lower triangular portion.
    /// The main diagonal of L consists (by convention) of
    /// ones, and is not explicitly stored.
    /// </returns>	
    public static double num_flops(int N)
    {
        // rougly 2/3*N^3

        double Nd = (double)N;

        return (2.0 * Nd * Nd * Nd / 3.0);
    }

    protected internal static double[] new_copy(double[] x)
    {
        double[] T = new double[x.Length];
        x.CopyTo(T, 0);
        return T;
    }


    protected internal static double[][] new_copy(double[][] A)
    {
        int M = A.Length;
        int N = A[0].Length;

        double[][] T = new double[M][];
        for (int i = 0; i < M; i++)
        {
            T[i] = new double[N];
        }

        for (int i = 0; i < M; i++)
        {
            A[i].CopyTo(T[i], 0);
        }

        return T;
    }



    public static int[] new_copy(int[] x)
    {
        int[] T = new int[x.Length];
        x.CopyTo(T, 0);
        return T;
    }

    protected internal static void insert_copy(double[][] B, double[][] A)
    {
        for (int i = 0; i < A.Length; i++)
        {
            A[i].CopyTo(B[i], 0);
        }
    }

    /// <summary>
    /// Initalize LU factorization from matrix.
    /// </summary>
    /// <param name="A">
    /// (in) the matrix to associate with this factorization.
    /// 
    /// </param>
    public LU(double[][] A)
    {
        _LU = new_copy(A);
        _pivot = new int[A.Length];

        factor(_LU, _pivot);
    }

    /// <summary>
    /// Solve a linear system, with pre-computed factorization.
    /// </summary>
    /// <param name="b">
    /// (in) the right-hand side.
    /// </param>
    /// <returns>
    /// solution vector.
    /// </returns>
    public virtual double[] solve(double[] b)
    {
        double[] x = new_copy(b);

        solve(_LU, _pivot, x);
        return x;
    }


    /// <summary>
    /// LU factorization (in place).
    /// </summary>
    /// <param name="A">
    /// (in/out) On input, the matrix to be factored.
    /// On output, the compact LU factorization.
    /// </param>
    /// <param name="pivot">
    /// (out) The pivot vector records the
    /// reordering of the rows of A during factorization.
    /// </param>
    /// <returns>
    /// 0, if OK, nozero value, othewise.
    /// </returns>
    public static int factor(double[][] A, int[] pivot)
    {
        int N = A.Length;
        int M = A[0].Length;

        int minMN = Math.Min(M, N);

        for (int j = 0; j < minMN; j++)
        {
            // find pivot in column j and  test for singularity.			
            int jp = j;

            double t = Math.Abs(A[j][j]);
            for (int i = j + 1; i < M; i++)
            {
                double ab = Math.Abs(A[i][j]);
                if (ab > t)
                {
                    jp = i;
                    t = ab;
                }
            }

            pivot[j] = jp;

            // jp now has the index of maximum element 
            // of column j, below the diagonal				
            if (A[jp][j] == 0)
                return 1;

            // factorization failed because of zero pivot
            if (jp != j)
            {
                // swap rows j and jp
                double[] tA = A[j];
                A[j] = A[jp];
                A[jp] = tA;
            }

            if (j < M - 1)
            {
                // compute elements j+1:M of jth column
                // note A(j,j), was A(jp,p) previously which was
                // guarranteed not to be zero (Label #1)
                //
                double recp = 1.0 / A[j][j];

                for (int k = j + 1; k < M; k++)
                    A[k][j] *= recp;
            }

            if (j < minMN - 1)
            {
                // rank-1 update to trailing submatrix:   E = E - x*y;
                //
                // E is the region A(j+1:M, j+1:N)
                // x is the column vector A(j+1:M,j)
                // y is row vector A(j,j+1:N)
                for (int ii = j + 1; ii < M; ii++)
                {
                    double[] Aii = A[ii];
                    double[] Aj = A[j];
                    double AiiJ = Aii[j];
                    for (int jj = j + 1; jj < N; jj++)
                        Aii[jj] -= AiiJ * Aj[jj];
                }
            }
        }

        return 0;
    }


    /// <summary>Solve a linear system, using a prefactored matrix
    /// in LU form.
    /// </summary>
    /// <param name="A">(in) the factored matrix in LU form. 
    /// </param>
    /// <param name="pivot">(in) the pivot vector which lists
    /// the reordering used during the factorization
    /// stage.
    /// </param>
    /// <param name="b">   (in/out) On input, the right-hand side.
    /// On output, the solution vector.
    /// 
    /// </param>
    public static void solve(double[][] A, int[] pvt, double[] b)
    {
        int M = A.Length;
        int N = A[0].Length;
        int ii = 0;

        for (int i = 0; i < M; i++)
        {
            int ip = pvt[i];
            double sum = b[ip];

            b[ip] = b[i];
            if (ii == 0)
            {
                for (int j = ii; j < i; j++)
                {
                    sum -= A[i][j] * b[j];
                }
            }
            else
            {
                if (sum == 0.0)
                {
                    ii = i;
                }
            }
            b[i] = sum;
        }

        for (int i = N - 1; i >= 0; i--)
        {
            double sum = b[i];
            for (int j = i + 1; j < N; j++)
            {
                sum -= A[i][j] * b[j];
            }
            b[i] = sum / A[i][i];
        }
    }
}

/// <summary>Estimate Pi by approximating the area of a circle.
/// How: generate N random numbers in the unit square, (0,0) to (1,1)
/// and see how are within a radius of 1 or less, i.e.
/// <pre>  
/// sqrt(x^2 + y^2) < r
/// </pre>
/// since the radius is 1.0, we can square both sides
/// and avoid a sqrt() computation:
/// <pre>
/// x^2 + y^2 <= 1.0
/// </pre>
/// this area under the curve is (Pi * r^2)/ 4.0,
/// and the area of the unit of square is 1.0,
/// so Pi can be approximated by 
/// <pre>
/// # points with x^2+y^2 < 1
/// Pi =~ 		--------------------------  * 4.0
/// total # points
/// </pre>
/// </summary>

public class MonteCarlo
{
    internal const int SEED = 113;

    public static double num_flops(int Num_samples)
    {
        // 3 flops in x^2+y^2 and 1 flop in random routine

        return ((double)Num_samples) * 4.0;
    }



    public static double integrate(int Num_samples)
    {
        Random R = new Random(SEED);


        int under_curve = 0;
        for (int count = 0; count < Num_samples; count++)
        {
            double x = R.nextDouble();
            double y = R.nextDouble();

            if (x * x + y * y <= 1.0)
                under_curve++;
        }

        return ((double)under_curve / Num_samples) * 4.0;
    }
}

/* Random.java based on Java Numerical Toolkit (JNT) Random.UniformSequence
class.  We do not use Java's own java.util.Random so that we can compare
results with equivalent C and Fortran coces.*/

public class Random
{
    /*------------------------------------------------------------------------------
    CLASS VARIABLES
    ------------------------------------------------------------------------------ */

    internal int seed = 0;

    private int[] _m;
    private int _i = 4;
    private int _j = 16;

    private const int mdig = 32;
    private const int one = 1;
    private int _m1;
    private int _m2;

    private double _dm1;

    private bool _haveRange = false;
    private double _left = 0.0;
    private double _right = 1.0;
    private double _width = 1.0;


    /* ------------------------------------------------------------------------------
    CONSTRUCTORS
    ------------------------------------------------------------------------------ */

    /// <summary>
    /// Initializes a sequence of uniformly distributed quasi random numbers with a
    /// seed based on the system clock.
    /// </summary>
    public Random()
    {
        initialize((int)System.DateTime.Now.Ticks);
    }

    /// <summary>
    /// Initializes a sequence of uniformly distributed quasi random numbers on a
    /// given half-open interval [left,right) with a seed based on the system
    /// clock.
    /// </summary>
    /// <param name="<B>left</B>">(double)<BR>
    /// The left endpoint of the half-open interval [left,right).
    /// </param>
    /// <param name="<B>right</B>">(double)<BR>
    /// The right endpoint of the half-open interval [left,right).
    /// </param>
    public Random(double left, double right)
    {
        initialize((int)System.DateTime.Now.Ticks);
        _left = left;
        _right = right;
        _width = right - left;
        _haveRange = true;
    }

    /// <summary>
    /// Initializes a sequence of uniformly distributed quasi random numbers with a
    /// given seed.
    /// </summary>
    /// <param name="<B>seed</B>">(int)<BR>
    /// The seed of the random number generator.  Two sequences with the same
    /// seed will be identical.
    /// </param>
    public Random(int seed)
    {
        initialize(seed);
    }

    /// <summary>Initializes a sequence of uniformly distributed quasi random numbers
    /// with a given seed on a given half-open interval [left,right).
    /// </summary>
    /// <param name="<B>seed</B>">(int)<BR>
    /// The seed of the random number generator.  Two sequences with the same
    /// seed will be identical.
    /// </param>
    /// <param name="<B>left</B>">(double)<BR>
    /// The left endpoint of the half-open interval [left,right).
    /// </param>
    /// <param name="<B>right</B>">(double)<BR>
    /// The right endpoint of the half-open interval [left,right).
    /// </param>
    public Random(int seed, double left, double right)
    {
        initialize(seed);
        _left = left;
        _right = right;
        _width = right - left;
        _haveRange = true;
    }

    /* ------------------------------------------------------------------------------
    PUBLIC METHODS
    ------------------------------------------------------------------------------ */

    /// <summary>
    /// Returns the next random number in the sequence.
    /// </summary>
    public double nextDouble()
    {
        int k;

        k = _m[_i] - _m[_j];
        if (k < 0)
            k += _m1;
        _m[_j] = k;

        if (_i == 0)
            _i = 16;
        else
            _i--;

        if (_j == 0)
            _j = 16;
        else
            _j--;

        if (_haveRange)
            return _left + _dm1 * (double)k * _width;
        else
            return _dm1 * (double)k;
    }

    /// <summary>
    /// Returns the next N random numbers in the sequence, as
    /// a vector.
    /// </summary>
    public void nextDoubles(double[] x)
    {
        int N = x.Length;
        int remainder = N & 3;

        if (_haveRange)
        {
            for (int count = 0; count < N; count++)
            {
                int k = _m[_i] - _m[_j];

                if (_i == 0)
                    _i = 16;
                else
                    _i--;

                if (k < 0)
                    k += _m1;
                _m[_j] = k;

                if (_j == 0)
                    _j = 16;
                else
                    _j--;

                x[count] = _left + _dm1 * (double)k * _width;
            }
        }
        else
        {
            for (int count = 0; count < remainder; count++)
            {
                int k = _m[_i] - _m[_j];

                if (_i == 0)
                    _i = 16;
                else
                    _i--;

                if (k < 0)
                    k += _m1;
                _m[_j] = k;

                if (_j == 0)
                    _j = 16;
                else
                    _j--;


                x[count] = _dm1 * (double)k;
            }

            for (int count = remainder; count < N; count += 4)
            {
                int k = _m[_i] - _m[_j];
                if (_i == 0)
                    _i = 16;
                else
                    _i--;
                if (k < 0)
                    k += _m1;
                _m[_j] = k;
                if (_j == 0)
                    _j = 16;
                else
                    _j--;
                x[count] = _dm1 * (double)k;


                k = _m[_i] - _m[_j];
                if (_i == 0)
                    _i = 16;
                else
                    _i--;
                if (k < 0)
                    k += _m1;
                _m[_j] = k;
                if (_j == 0)
                    _j = 16;
                else
                    _j--;
                x[count + 1] = _dm1 * (double)k;


                k = _m[_i] - _m[_j];
                if (_i == 0)
                    _i = 16;
                else
                    _i--;
                if (k < 0)
                    k += _m1;
                _m[_j] = k;
                if (_j == 0)
                    _j = 16;
                else
                    _j--;
                x[count + 2] = _dm1 * (double)k;


                k = _m[_i] - _m[_j];
                if (_i == 0)
                    _i = 16;
                else
                    _i--;
                if (k < 0)
                    k += _m1;
                _m[_j] = k;
                if (_j == 0)
                    _j = 16;
                else
                    _j--;
                x[count + 3] = _dm1 * (double)k;
            }
        }
    }

    /*----------------------------------------------------------------------------
    PRIVATE METHODS
    ------------------------------------------------------------------------ */

    private void initialize(int seed)
    {
        // First the initialization of the member variables;
        _m1 = (one << mdig - 2) + ((one << mdig - 2) - one);
        _m2 = one << mdig / 2;
        _dm1 = 1.0 / (double)_m1;

        int jseed, k0, k1, j0, j1, iloop;

        this.seed = seed;

        _m = new int[17];

        jseed = System.Math.Min(System.Math.Abs(seed), _m1);
        if (jseed % 2 == 0)
            --jseed;
        k0 = 9069 % _m2;
        k1 = 9069 / _m2;
        j0 = jseed % _m2;
        j1 = jseed / _m2;
        for (iloop = 0; iloop < 17; ++iloop)
        {
            jseed = j0 * k0;
            j1 = (jseed / _m2 + j0 * k1 + j1 * k0) % (_m2 / 2);
            j0 = jseed % _m2;
            _m[iloop] = j0 + _m2 * j1;
        }
        _i = 4;
        _j = 16;
    }
}

public class SOR
{
    public static double num_flops(int M, int N, int num_iterations)
    {
        double Md = (double)M;
        double Nd = (double)N;
        double num_iterD = (double)num_iterations;

        return (Md - 1) * (Nd - 1) * num_iterD * 6.0;
    }

    public static void execute(double omega, double[][] G, int num_iterations)
    {
        int M = G.Length;
        int N = G[0].Length;

        double omega_over_four = omega * 0.25;
        double one_minus_omega = 1.0 - omega;

        // update interior points
        //
        int Mm1 = M - 1;
        int Nm1 = N - 1;
        for (int p = 0; p < num_iterations; p++)
        {
            for (int i = 1; i < Mm1; i++)
            {
                double[] Gi = G[i];
                double[] Gim1 = G[i - 1];
                double[] Gip1 = G[i + 1];
                for (int j = 1; j < Nm1; j++)
                    Gi[j] = omega_over_four * (Gim1[j] + Gip1[j] + Gi[j - 1] + Gi[j + 1]) + one_minus_omega * Gi[j];
            }
        }
    }
}

public class SparseCompRow
{
    // multiple iterations used to make kernel 
    // have roughly same granulairty as other 
    // Scimark kernels	
    public static double num_flops(int N, int nz, int num_iterations)
    {
        /* Note that if nz does not divide N evenly, then the
        actual number of nonzeros used is adjusted slightly.
        */
        int actual_nz = (nz / N) * N;
        return ((double)actual_nz) * 2.0 * ((double)num_iterations);
    }

    /// <summary>
    ///  computes  a matrix-vector multiply with a sparse matrix
    ///  held in compress-row format.  If the size of the matrix
    ///  in MxN with nz nonzeros, then the val[] is the nz nonzeros,
    ///  with its ith entry in column col[i].  The integer vector row[]
    ///  is of size M+1 and row[i] points to the begining of the
    ///  ith row in col[].  
    public static void matmult(double[] y, double[] val, int[] row, int[] col, double[] x, int NUM_ITERATIONS)
    {
        int M = row.Length - 1;

        for (int reps = 0; reps < NUM_ITERATIONS; reps++)
        {
            for (int r = 0; r < M; r++)
            {
                double sum = 0.0;
                int rowR = row[r];
                int rowRp1 = row[r + 1];
                for (int i = rowR; i < rowRp1; i++)
                    sum += x[col[i]] * val[i];
                y[r] = sum;
            }
        }
    }
}

public static class kernel
{
    public static void benchFFT()
    {
        Random R = new Random(Constants.RANDOM_SEED);
        int N = Constants.FFT_SIZE;
        long Iterations = 200000;

        double[] x = RandomVector(2 * N, R);
        innerFFT(x, Iterations);
        validateFFT(N, x);
    }

    private static void innerFFT(double[] x, long Iterations)
    {
        for (int i = 0; i < Iterations; i++)
        {
            FFT.transform(x); // forward transform
            FFT.inverse(x);   // backward transform
        }
    }

    private static void validateFFT(int N, double[] x)
    {
        const double EPS = 1.0e-10;
        if (FFT.test(x) / N > EPS)
        {
            throw new Exception("FFT failed to validate");
        }
    }

    public static double measureFFT(int N, double mintime, Random R)
    {
        // initialize FFT data as complex (N real/img pairs)
        double[] x = RandomVector(2 * N, R);

        long cycles = 1;
        Stopwatch Q = new Stopwatch();
        while (true)
        {
            Q.start();
            innerFFT(x, cycles);
            Q.stop();
            if (Q.read() >= mintime)
                break;

            cycles *= 2;
        }

        validateFFT(N, x);

        // approx Mflops
        return FFT.num_flops(N) * cycles / Q.read() * 1.0e-6;
    }

    public static void benchSOR()
    {
        int N = Constants.SOR_SIZE;
        Random R = new Random(Constants.RANDOM_SEED);
        int Iterations = 200000;
        double[][] G = RandomMatrix(N, N, R);
        SOR.execute(1.25, G, Iterations);

    }

    public static double measureSOR(int N, double min_time, Random R)
    {
        double[][] G = RandomMatrix(N, N, R);
        Stopwatch Q = new Stopwatch();
        int cycles = 1;
        while (true)
        {
            Q.start();
            SOR.execute(1.25, G, cycles);
            Q.stop();
            if (Q.read() >= min_time)
                break;

            cycles *= 2;
        }

        // approx Mflops
        return SOR.num_flops(N, N, cycles) / Q.read() * 1.0e-6;
    }

    public static void benchMonteCarlo()
    {
        Random R = new Random(Constants.RANDOM_SEED);
        int Iterations = 400000000;
        MonteCarlo.integrate(Iterations);

    }

    public static double measureMonteCarlo(double min_time, Random R)
    {
        Stopwatch Q = new Stopwatch();

        int cycles = 1;
        while (true)
        {
            Q.start();
            MonteCarlo.integrate(cycles);
            Q.stop();
            if (Q.read() >= min_time)
                break;

            cycles *= 2;
        }

        // approx Mflops
        return MonteCarlo.num_flops(cycles) / Q.read() * 1.0e-6;
    }

    public static void benchSparseMult()
    {
        int N = Constants.SPARSE_SIZE_M;
        int nz = Constants.SPARSE_SIZE_nz;
        int Iterations = 1000000;
        Random R = new Random(Constants.RANDOM_SEED);

        double[] x = RandomVector(N, R);
        double[] y = new double[N];
        int nr = nz / N; // average number of nonzeros per row
        int anz = nr * N; // _actual_ number of nonzeros
        double[] val = RandomVector(anz, R);
        int[] col = new int[anz];
        int[] row = new int[N + 1];

        row[0] = 0;
        for (int r = 0; r < N; r++)
        {
            // initialize elements for row r

            int rowr = row[r];
            row[r + 1] = rowr + nr;
            int step = r / nr;
            if (step < 1)
                step = 1;
            // take at least unit steps

            for (int i = 0; i < nr; i++)
                col[rowr + i] = i * step;
        }

        SparseCompRow.matmult(y, val, row, col, x, Iterations);

    }

    public static double measureSparseMatmult(int N, int nz, double min_time, Random R)
    {
        // initialize vector multipliers and storage for result
        // y = A*y;

        double[] x = RandomVector(N, R);
        double[] y = new double[N];

        // initialize square sparse matrix
        //
        // for this test, we create a sparse matrix wit M/nz nonzeros
        // per row, with spaced-out evenly between the begining of the
        // row to the main diagonal.  Thus, the resulting pattern looks
        // like
        //             +-----------------+
        //             +*                +
        //             +***              +
        //             +* * *            +
        //             +** *  *          +
        //             +**  *   *        +
        //             +* *   *   *      +
        //             +*  *   *    *    +
        //             +*   *    *    *  +
        //             +-----------------+
        //
        // (as best reproducible with integer artihmetic)
        // Note that the first nr rows will have elements past
        // the diagonal.

        int nr = nz / N; // average number of nonzeros per row
        int anz = nr * N; // _actual_ number of nonzeros


        double[] val = RandomVector(anz, R);
        int[] col = new int[anz];
        int[] row = new int[N + 1];

        row[0] = 0;
        for (int r = 0; r < N; r++)
        {
            // initialize elements for row r

            int rowr = row[r];
            row[r + 1] = rowr + nr;
            int step = r / nr;
            if (step < 1)
                step = 1;
            // take at least unit steps

            for (int i = 0; i < nr; i++)
                col[rowr + i] = i * step;
        }

        Stopwatch Q = new Stopwatch();

        int cycles = 1;
        while (true)
        {
            Q.start();
            SparseCompRow.matmult(y, val, row, col, x, cycles);
            Q.stop();
            if (Q.read() >= min_time)
                break;

            cycles *= 2;
        }

        // approx Mflops
        return SparseCompRow.num_flops(N, nz, cycles) / Q.read() * 1.0e-6;
    }

    public static void benchmarkLU()
    {
        int N = Constants.LU_SIZE;
        Random R = new Random(Constants.RANDOM_SEED);
        int Iterations = 20000;

        double[][] A = RandomMatrix(N, N, R);
        double[][] lu = new double[N][];
        for (int i = 0; i < N; i++)
        {
            lu[i] = new double[N];
        }
        int[] pivot = new int[N];

        for (int i = 0; i < Iterations; i++)
        {
            CopyMatrix(lu, A);
            LU.factor(lu, pivot);
        }

        validateLU(N, R, lu, A, pivot);
    }

    public static void validateLU(int N, Random R, double[][] lu, double[][] A, int[] pivot)
    {
        // verify that LU is correct
        double[] b = RandomVector(N, R);
        double[] x = NewVectorCopy(b);

        LU.solve(lu, pivot, x);

        const double EPS = 1.0e-12;
        if (normabs(b, matvec(A, x)) / N > EPS)
        {
            throw new Exception("LU failed to validate");
        }
    }
    public static double measureLU(int N, double min_time, Random R)
    {
        // compute approx Mlfops, or O if LU yields large errors

        double[][] A = RandomMatrix(N, N, R);
        double[][] lu = new double[N][];
        for (int i = 0; i < N; i++)
        {
            lu[i] = new double[N];
        }
        int[] pivot = new int[N];

        Stopwatch Q = new Stopwatch();

        int cycles = 1;
        while (true)
        {
            Q.start();
            for (int i = 0; i < cycles; i++)
            {
                CopyMatrix(lu, A);
                LU.factor(lu, pivot);
            }
            Q.stop();
            if (Q.read() >= min_time)
                break;

            cycles *= 2;
        }

        validateLU(N, R, lu, A, pivot);

        return LU.num_flops(N) * cycles / Q.read() * 1.0e-6;
    }

    private static double[] NewVectorCopy(double[] x)
    {
        int N = x.Length;

        double[] y = new double[N];
        for (int i = 0; i < N; i++)
            y[i] = x[i];

        return y;
    }

    private static void CopyVector(double[] B, double[] A)
    {
        int N = A.Length;

        for (int i = 0; i < N; i++)
            B[i] = A[i];
    }

    private static double normabs(double[] x, double[] y)
    {
        int N = x.Length;
        double sum = 0.0;

        for (int i = 0; i < N; i++)
            sum += System.Math.Abs(x[i] - y[i]);

        return sum;
    }

    private static void CopyMatrix(double[][] B, double[][] A)
    {
        int M = A.Length;
        int N = A[0].Length;

        int remainder = N & 3; // N mod 4;

        for (int i = 0; i < M; i++)
        {
            double[] Bi = B[i];
            double[] Ai = A[i];
            for (int j = 0; j < remainder; j++)
                Bi[j] = Ai[j];
            for (int j = remainder; j < N; j += 4)
            {
                Bi[j] = Ai[j];
                Bi[j + 1] = Ai[j + 1];
                Bi[j + 2] = Ai[j + 2];
                Bi[j + 3] = Ai[j + 3];
            }
        }
    }

    private static double[][] RandomMatrix(int M, int N, Random R)
    {
        double[][] A = new double[M][];
        for (int i = 0; i < M; i++)
        {
            A[i] = new double[N];
        }

        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                A[i][j] = R.nextDouble();
        return A;
    }

    private static double[] RandomVector(int N, Random R)
    {
        double[] A = new double[N];

        for (int i = 0; i < N; i++)
            A[i] = R.nextDouble();
        return A;
    }

    private static double[] matvec(double[][] A, double[] x)
    {
        int N = x.Length;
        double[] y = new double[N];

        matvec(A, x, y);

        return y;
    }

    private static void matvec(double[][] A, double[] x, double[] y)
    {
        int M = A.Length;
        int N = A[0].Length;

        for (int i = 0; i < M; i++)
        {
            double sum = 0.0;
            double[] Ai = A[i];
            for (int j = 0; j < N; j++)
                sum += Ai[j] * x[j];

            y[i] = sum;
        }
    }
}


/// <summary>
/// Provides a stopwatch to measure elapsed time.
/// </summary>
/// <author> 
/// Roldan Pozo
/// </author>
/// <version> 
/// 14 October 1997, revised 1999-04-24
/// </version>
/// 
public class Stopwatch
{
    private bool _running;
    private double _last_time;
    private double _total;
    internal readonly TimeSpan Elapsed;

    /// 
    /// <summary>R
    /// eturn system time (in seconds)
    /// </summary>
    public static double seconds()
    {
        return (System.DateTime.Now.Ticks * 1.0E-7);
    }

    public virtual void reset()
    {
        _running = false;
        _last_time = 0.0;
        _total = 0.0;
    }

    public Stopwatch()
    {
        reset();
    }

    /// 
    /// <summary>
    /// Start (and reset) timer
    /// </summary>
    public virtual void start()
    {
        if (!_running)
        {
            _running = true;
            _total = 0.0;
            _last_time = seconds();
        }
    }

    /// 
    /// <summary>
    /// Resume timing, after stopping.  (Does not wipe out accumulated times.)
    /// </summary>
    public virtual void resume()
    {
        if (!_running)
        {
            _last_time = seconds();
            _running = true;
        }
    }

    /// 
    /// <summary>
    /// Stop timer
    /// </summary>
    public virtual double stop()
    {
        if (_running)
        {
            _total += seconds() - _last_time;
            _running = false;
        }
        return _total;
    }

    /// 
    /// <summary>
    /// return the elapsed time (in seconds)
    /// </summary>
    public virtual double read()
    {
        if (_running)
        {
            _total += seconds() - _last_time;
            _last_time = seconds();
        }
        return _total;
    }
}