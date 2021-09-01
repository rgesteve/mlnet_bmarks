using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.ML;
using Microsoft.ML.Calibrators;
using Microsoft.ML.CommandLine;
using Microsoft.ML.Data;
using Microsoft.ML.Data.Conversion;
using Microsoft.ML.EntryPoints;
using Microsoft.ML.Internal.Internallearn;
using Microsoft.ML.Internal.Utilities;
using Microsoft.ML.Model;
using Microsoft.ML.Runtime;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.FastTree;
using Microsoft.ML.Transforms;

namespace simpleDataConversionApp
{
    public class RegressionPrediction
    {
        public float target;
        public float score;
    }

    class Program
    {
        public static IDataView[] LoadData(MLContext mlContext, string dataset, string task, string label = "target", char separator = ',')
        {
            List<IDataView> dataList = new List<IDataView>();
            System.IO.StreamReader file = new System.IO.StreamReader($"{dataset}_train.csv");
            string header = file.ReadLine();
            file.Close();
            string[] headerArray = header.Split(separator);
            List<TextLoader.Column> columns = new List<TextLoader.Column>();
            foreach (string column in headerArray)
            {
                if (column == label)
                {
                    if (task == "binary")
                        columns.Add(new TextLoader.Column(column, DataKind.Boolean, Array.IndexOf(headerArray, column)));
                    else
                        columns.Add(new TextLoader.Column(column, DataKind.Single, Array.IndexOf(headerArray, column)));
                }
                else
                {
                    if (task == "recommendation")
                        columns.Add(new TextLoader.Column(column, DataKind.UInt32, Array.IndexOf(headerArray, column)));
                    else
                        columns.Add(new TextLoader.Column(column, DataKind.Single, Array.IndexOf(headerArray, column)));
                }
            }

            var loader = mlContext.Data.CreateTextLoader(
                separatorChar: separator,
                hasHeader: true,
                columns: columns.ToArray()
            );
            dataList.Add(mlContext.Data.Cache(loader.Load($"{dataset}_train.csv")));
            dataList.Add(mlContext.Data.Cache(loader.Load($"{dataset}_test.csv")));

            return dataList.ToArray();
        }

        public static string[] GetFeaturesArray(IDataView data, string labelName = "target")
        {
            List<string> featuresList = new List<string>();
            var nColumns = data.Schema.Count;
            var columnsEnumerator = data.Schema.GetEnumerator();
            for (int i = 0; i < nColumns; i++)
            {
                columnsEnumerator.MoveNext();
                if (columnsEnumerator.Current.Name != labelName)
                    featuresList.Add(columnsEnumerator.Current.Name);
            }

            return featuresList.ToArray();
        }

        [DllImport("_oneDALWrapper.so", EntryPoint = "linearRegressionSingle")]
        private unsafe static extern void LinearRegressionSingle(void* trainData, void* trainLabel, void* testData, void* predictions, int nTrainRows, int nTestRows, int nColumns);

        [DllImport("_oneDALWrapper.so", EntryPoint = "randomForestRegressionSingle")]
        private unsafe static extern void RandomForestRegressionSingle(void* trainData, void* trainLabel, void* testData, void* predictions, int nTrainRows, int nTestRows, int nColumns);

        private static List<double> MLNETRegression(MLContext mlContext, Func<IDataView, ITransformer> fitMethod, IDataView trainingData, IDataView testingData)
        {
            var t1 = System.Diagnostics.Stopwatch.StartNew();
            ITransformer model = fitMethod(trainingData);
            IDataView testingPredictions = model.Transform(testingData);

            t1.Stop();
            var t2 = System.Diagnostics.Stopwatch.StartNew();

            List<double> metricsList = new List<double>();
            var metrics = mlContext.Regression.Evaluate(testingPredictions, labelColumnName: "target", scoreColumnName: "Score");
            metricsList.Add(metrics.MeanAbsoluteError);
            metricsList.Add(metrics.RootMeanSquaredError);
            metricsList.Add(metrics.RSquared);

            t2.Stop();
            Console.WriteLine($"Execution     : {t1.Elapsed.TotalMilliseconds}");
            Console.WriteLine($"Evaluation    : {t2.Elapsed.TotalMilliseconds}");

            return metricsList;
        }

        private static List<double> MLNETLinearRegression(MLContext mlContext, IDataView trainingData, IDataView testingData)
        {
            var trainer = mlContext.Regression.Trainers.Ols(labelColumnName: "target", featureColumnName: "Features");
            return MLNETRegression(mlContext, trainer.Fit, trainingData, testingData);
        }

        private static List<double> MLNETRandomForestRegression(MLContext mlContext, IDataView trainingData, IDataView testingData)
        {
            var options = new FastForestRegressionTrainer.Options()
            {
                LabelColumnName = "target",
                BaggingExampleFraction  = 0.7,
                FeatureColumnName = "Features",
                NumberOfThreads = 48,
                BaggingSize = 0,
                FeatureFraction = 1,
                FeatureFractionPerSplit = 0.5,
                Seed = 777,
                FeatureSelectionSeed = 777,
                HistogramPoolSize  = 256,
                MinimumExampleCountPerLeaf = 1,
                MinimumExampleFractionForCategoricalSplit = 0,
                NumberOfLeaves = 1000,
                NumberOfTrees = 100,
                ShuffleLabels = false
            };
            var trainer = mlContext.Regression.Trainers.FastForest(options);
            return MLNETRegression(mlContext, trainer.Fit, trainingData, testingData);
        }

        private static List<double> OneDALRegression(MLContext mlContext, IDataView trainingData, IDataView testingData, string algorithm)
        {
            var t1 = System.Diagnostics.Stopwatch.StartNew();

            float[][] rawTrainingData = trainingData.GetColumn<float[]>("Features").ToArray();
            float[][] rawTestingData = testingData.GetColumn<float[]>("Features").ToArray();
            float[] rawTrainingLabel = trainingData.GetColumn<float>("target").ToArray();

            int nTrainingRows = (int)trainingData.GetRowCount();
            int nTestingRows = (int)testingData.GetRowCount();
            int nColumns = trainingData.Schema.Count - 2;

            t1.Stop();
            var t2 = System.Diagnostics.Stopwatch.StartNew();

            float[] trainingDataCopy = new float[nTrainingRows * nColumns];
            for (int i = 0; i < nTrainingRows; ++i)
            {
                for (int j = 0; j < nColumns; ++j)
                {
                    trainingDataCopy[i * nColumns + j] = rawTrainingData[i][j];
                }
            }
            float[] testingDataCopy = new float[nTestingRows * nColumns];
            for (int i = 0; i < nTestingRows; ++i)
            {
                for (int j = 0; j < nColumns; ++j)
                {
                    testingDataCopy[i * nColumns + j] = rawTestingData[i][j];
                }
            }
            float[] trainingLabelCopy = new float[nTrainingRows];
            for (int i = 0; i < nTrainingRows; ++i)
            {
                trainingLabelCopy[i] = rawTrainingLabel[i];
            }
            float[] predictions = new float[nTestingRows];

            t2.Stop();
            var t3 = System.Diagnostics.Stopwatch.StartNew();

            unsafe
            {
                fixed (void* trainingDataPtr = &trainingDataCopy[0], testingDataPtr = &testingDataCopy[0], trainingLabelPtr = &trainingLabelCopy[0], predictionsPtr = &predictions[0])
                {
                    if (algorithm == "LinearRegression")
                    {
                        LinearRegressionSingle(trainingDataPtr, trainingLabelPtr, testingDataPtr, predictionsPtr, nTrainingRows, nTestingRows, nColumns);
                    }
                    else if (algorithm == "RandomForest")
                    {
                        RandomForestRegressionSingle(trainingDataPtr, trainingLabelPtr, testingDataPtr, predictionsPtr, nTrainingRows, nTestingRows, nColumns);
                    }
                }
            }

            t3.Stop();
            var t4 = System.Diagnostics.Stopwatch.StartNew();

            float[] rawTestingLabel = testingData.GetColumn<float>("target").ToArray();
            RegressionPrediction[] predictionArray = new RegressionPrediction[nTestingRows];
            for (int i = 0; i < nTestingRows; ++i)
            {
                predictionArray[i] = new RegressionPrediction() {target = rawTestingLabel[i], score = predictions[i]};
            }
            IDataView predictionDataView = mlContext.Data.LoadFromEnumerable(predictionArray);

            List<double> metricsList = new List<double>();
            var metrics = mlContext.Regression.Evaluate(predictionDataView, labelColumnName: "target", scoreColumnName: "score");
            metricsList.Add(metrics.MeanAbsoluteError);
            metricsList.Add(metrics.RootMeanSquaredError);
            metricsList.Add(metrics.RSquared);

            t4.Stop();

            Console.WriteLine($"Get raw data  : {t1.Elapsed.TotalMilliseconds}");
            Console.WriteLine($"Copying       : {t2.Elapsed.TotalMilliseconds}");
            Console.WriteLine($"Execution     : {t3.Elapsed.TotalMilliseconds}");
            Console.WriteLine($"Evaluation    : {t4.Elapsed.TotalMilliseconds}");

            return metricsList;
        }

        static void Main(string[] args)
        {
            // args[0] - dataset prefix
            // args[1] - task (regression/binary/multi)
            // args[2] - algorithm
            // args[3] - onedal / mlnet
            Console.WriteLine($"{args[0]} dataset, {args[1]} task, {args[2]} algorithm, {args[3]} library");
            var tg = System.Diagnostics.Stopwatch.StartNew();
            var t0 = System.Diagnostics.Stopwatch.StartNew();

            MLContext mlContext = new MLContext(seed: 42);
            IDataView[] data = LoadData(mlContext, args[0], args[1]);

            var featuresArray = GetFeaturesArray(data[0]);
            var preprocessingPipeline = mlContext.Transforms.Concatenate("Features", featuresArray);
            var preprocessingModel = preprocessingPipeline.Fit(data[0]);
            IDataView preprocessedTrainingData = mlContext.Data.Cache(preprocessingModel.Transform(data[0]));
            IDataView preprocessedTestingData = mlContext.Data.Cache(preprocessingModel.Transform(data[1]));

            t0.Stop();
            Console.WriteLine("Times[ms]:");
            Console.WriteLine($"Preprocessing : {t0.Elapsed.TotalMilliseconds}");

            List<double> metrics = null;
            if (args[3] == "onedal")
            {
                metrics = OneDALRegression(mlContext, preprocessedTrainingData, preprocessedTestingData, args[2]);
            }
            else
            {
                if (args[2] == "LinearRegression")
                {
                    metrics = MLNETLinearRegression(mlContext, preprocessedTrainingData, preprocessedTestingData);
                }
                else if (args[2] == "RandomForest")
                {
                    metrics = MLNETRandomForestRegression(mlContext, preprocessedTrainingData, preprocessedTestingData);
                }
            }

            tg.Stop();
            Console.WriteLine($"All           : {tg.Elapsed.TotalMilliseconds}");
            Console.WriteLine("");
            Console.WriteLine($"Training shape : ({preprocessedTrainingData.GetRowCount()}, {preprocessedTrainingData.Schema.Count - 2})");
            Console.WriteLine($"Testing shape  : ({preprocessedTestingData.GetRowCount()}, {preprocessedTestingData.Schema.Count - 2})");
            Console.WriteLine("");

            Console.WriteLine("Metrics:");
            Console.WriteLine($"MeanAbsoluteError    : {metrics[0]}");
            Console.WriteLine($"RootMeanSquaredError : {metrics[1]}");
            Console.WriteLine($"RSquared             : {metrics[2]}");
            Console.WriteLine("");
        }
    }
}
