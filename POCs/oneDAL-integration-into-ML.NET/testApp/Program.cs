using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;

class Program
{
    public static IDataView[] LoadData(MLContext mlContext, string dataset, string label = "target", char separator = ',')
    {
        System.IO.StreamReader file = new System.IO.StreamReader($"{dataset}_train.csv");
        string header = file.ReadLine();
        file.Close();
        string[] headerArray = header.Split(separator);
        List<TextLoader.Column> columns = new List<TextLoader.Column>();
        foreach (string column in headerArray)
        {
            columns.Add(new TextLoader.Column(column, DataKind.Single, Array.IndexOf(headerArray, column)));
        }

        var loader = mlContext.Data.CreateTextLoader(
            separatorChar: separator,
            hasHeader: true,
            columns: columns.ToArray()
        );
        List<IDataView> dataList = new List<IDataView>();
        dataList.Add(loader.Load($"{dataset}_train.csv"));
        dataList.Add(loader.Load($"{dataset}_test.csv"));
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

    static void Main(string[] args)
    {
        var tg = System.Diagnostics.Stopwatch.StartNew();
        var t0 = System.Diagnostics.Stopwatch.StartNew();
        MLContext mlContext = new MLContext();
        var data = LoadData(mlContext, args[0]);
        var featuresArray = GetFeaturesArray(data[0]);
        var preprocessingModel = mlContext.Transforms.Concatenate("Features", featuresArray);
        var trainingData = preprocessingModel.Fit(data[0]).Transform(data[0]);
        var testingData = preprocessingModel.Fit(data[0]).Transform(data[1]);
        t0.Stop();

        var t1 = System.Diagnostics.Stopwatch.StartNew();
        ITransformer model;
        if (args[1] == "onedal")
        {
            var trainer = mlContext.Regression.Trainers.LinReg(labelColumnName: "target", featureColumnName: "Features");
            model = trainer.Fit(trainingData);
        }
        else
        {
            var trainer = mlContext.Regression.Trainers.Ols(labelColumnName: "target", featureColumnName: "Features");
            model = trainer.Fit(trainingData);
        }
        t1.Stop();

        var t2 = System.Diagnostics.Stopwatch.StartNew();
        IDataView predictions = model.Transform(testingData);
        t2.Stop();

        var t3 = System.Diagnostics.Stopwatch.StartNew();
        List<double> metricsList = new List<double>();
        var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: "target", scoreColumnName: "Score");
        t3.Stop();
        tg.Stop();

        Console.WriteLine("Impl.,Dataset,All time[ms],Reading time[ms],Fitting time[ms],Prediction time[ms],Evaluation time[ms],MAE,RMSE,R2");
        Console.Write($"{args[1]},{args[0]},{tg.Elapsed.TotalMilliseconds},{t0.Elapsed.TotalMilliseconds},{t1.Elapsed.TotalMilliseconds},{t2.Elapsed.TotalMilliseconds},{t3.Elapsed.TotalMilliseconds}");
        Console.Write($"{metrics.MeanAbsoluteError},{metrics.RootMeanSquaredError},{metrics.RSquared}\n");
    }
}
