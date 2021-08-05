#define UseOptions // or NoOptions
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Diagnostics;

namespace M2Demo
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(LogLevel.Debug);
            loggerFactory.AddDebug(LogLevel.Debug);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

#if NoOptions
            #region UseWebSockets
            app.UseWebSockets();
            #endregion
#endif
#if UseOptions
            #region UseWebSocketsOptions
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);
            #endregion
#endif
            #region AcceptWebSocket
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await Matrix(context, webSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }

            });
#endregion
            app.UseFileServer();
        }
#region Matrix
			
        private async Task Matrix(HttpContext context, WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
				var requestSize = System.Text.Encoding.Default.GetString(buffer, 0, result.Count);

                int num = 0;

				if (requestSize != null) {
					num = Convert.ToInt32(requestSize);
				}
				
				if (num == 0)
				{
					var str = "{\"b\":\"0\",\"v\":0,\"i\":0}";
					var b = Encoding.ASCII.GetBytes(str);
					await webSocket.SendAsync(new ArraySegment<byte>(b, 0, b.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
				} else {
					 // two source matrix
                    float[] matrixA = MatrixMultiplication.RandomMatrix(num);
                    float[] matrixB = MatrixMultiplication.RandomMatrix(num);

                    // matrix for storing return value
                    float[] matrixC = MatrixMultiplication.RandomMatrix(num);
                    float[] matrixD = MatrixMultiplication.RandomMatrix(num);
                    float[] matrixE = MatrixMultiplication.RandomMatrix(num);
					
                    double timeV = MatrixMultiplication.benchVectorTMatrixMultiplication(matrixA, matrixB, ref matrixD, num);
                    double timeI = MatrixMultiplication.benchIntrinsicMatrixMultiplication(matrixA, matrixB, ref matrixE, num);;
                    					
					System.Console.WriteLine("num = " + num + " timeV = " + timeV + " timeI = " + timeI);
					
					var str = "{\"b\":\"" + num + "\",\"v\":" + timeV + ",\"i\":" + timeI + "}";
					System.Console.WriteLine(str);
					var b = Encoding.ASCII.GetBytes(str);
					await webSocket.SendAsync(new ArraySegment<byte>(b, 0, b.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
				}
				
				result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
				
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
#endregion
    }

    class MatrixMultiplication
    {
        private static int iteration = 50000;
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
            if (!Avx.IsSupported)
            {
                return;
            }

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
}