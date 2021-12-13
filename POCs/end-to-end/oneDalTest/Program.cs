﻿using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.ML;
using Microsoft.ML.Data;

using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;

class Program
{
    static string datasetLocation = @"";
    public static IDataView[] LoadData(MLContext mlContext, string dataset, string label = "target", char separator = ',')
    {
        System.IO.StreamReader file = new System.IO.StreamReader($"{datasetLocation}{dataset}_train.csv");
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
        dataList.Add(loader.Load($"{datasetLocation}{dataset}_train.csv"));
        dataList.Add(loader.Load($"{datasetLocation}{dataset}_test.csv"));
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
        Console.WriteLine("Waiting"); 

        Console.ReadKey();
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
        var trainer = mlContext.Regression.Trainers.Ols(labelColumnName: "target", featureColumnName: "Features");
        var model = trainer.Fit(trainingData);
        t1.Stop();

        var t2 = System.Diagnostics.Stopwatch.StartNew();
        IDataView predictions = model.Transform(testingData);
        t2.Stop();

        var t3 = System.Diagnostics.Stopwatch.StartNew();
        List<double> metricsList = new List<double>();
        var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: "target", scoreColumnName: "Score");
        t3.Stop();
        tg.Stop();

        Console.WriteLine("Dataset,All time[ms],Reading time[ms],Fitting time[ms],Prediction time[ms],Evaluation time[ms],MAE,RMSE,R2");
        Console.Write($"{args[0]},{tg.Elapsed.TotalMilliseconds},{t0.Elapsed.TotalMilliseconds},{t1.Elapsed.TotalMilliseconds},{t2.Elapsed.TotalMilliseconds},{t3.Elapsed.TotalMilliseconds},");
        Console.Write($"{metrics.MeanAbsoluteError},{metrics.RootMeanSquaredError},{metrics.RSquared}\n");
    }

    static private LBFGSTest()
    {
	MLContext mlCtxt = new MLContext();

	// Should split this in training and testing so that we can evaluate the result
	var trainData = mlCtxt.Data.LoadFromTextFile<WineRecord>("winequality-red-scaled-classify.csv", separatorChar: ',', hasHeader: true);
	var dataPipe = mlCtxt.Transforms.Concatenate("Features", nameof(WineRecord.Alcohol))
	    	                   .Append(mlCtxt.BinaryClassification.Trainers.LbfgsLogisticRegression(labelColumnName : nameof(WineRecord.Quality)));
	var d = dataPipe.Fit(trainData);
    }

    class WineRecord
    {
	[LoadColumn(0), ColumnName("fixed acidity")]
	public Single FixedAcidity { get; set; }

    	[LoadColumn(1), ColumnName("volatile acidity")]
	public Single VolatileAcidity { get; set; }

    	[LoadColumn(2), ColumnName("citric acid")]
	public Single CitricAcid { get; set; }

    	[LoadColumn(3), ColumnName("residual sugar")]
	public Single ResidualSugar { get; set; }
    
	[LoadColumn(4), ColumnName("chlorides")]
	public Single Chlorides { get; set; }
    
	[LoadColumn(5), ColumnName("free sulfur dioxide")]
	public Single FreeSulfurDioxide { get; set; }

    	[LoadColumn(6), ColumnName("total sulfur dioxide")]
	public Single TotalSulfurDioxide { get; set; }

    	[LoadColumn(7), ColumnName("density")]
	public Single Density { get; set; }
    
	[LoadColumn(8), ColumnName("pH")]
	public Single Ph { get; set; }
    
	[LoadColumn(9), ColumnName("sulphates")]
	public Single Sulphates { get; set; }
    
	[LoadColumn(10), ColumnName("alcohol")]
	public Single Alcohol { get; set; }
      
	[LoadColumn(11), ColumnName("quality")]
	public bool Quality { get; set; }
    };
}
