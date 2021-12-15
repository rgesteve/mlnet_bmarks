using System;
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
	if (args.Length == 0) {
	   Console.WriteLine("Testing LBFGS...");
	   LBFGSTest();
	   Environment.Exit(0);
	}

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

    static private void LBFGSTest()
    {
	MLContext mlCtxt = new MLContext();

	// Should split this in training and testing so that we can evaluate the result
	var trainData = mlCtxt.Data.LoadFromTextFile<WineRecord>("winequality-red-scaled-classify.csv", separatorChar: ',', hasHeader: true);
	var dataPipe = mlCtxt.Transforms.Concatenate("Features", nameof(WineRecord.Alcohol))
	    	                   .Append(mlCtxt.BinaryClassification.Trainers.LbfgsLogisticRegression(labelColumnName : nameof(WineRecord.Quality)));
   	var split = mlCtxt.Data.TrainTestSplit(trainData, testFraction: 0.1);
	var model = dataPipe.Fit(split.TrainSet);
	var evalData = model.Transform(split.TestSet);
	var metrics = mlCtxt.BinaryClassification.Evaluate(evalData, labelColumnName : nameof(WineRecord.Quality));

	Console.WriteLine($"The accuracy of the prediction is {metrics.Accuracy}.");
	
    }

    class WineRecord
    {
	[LoadColumn(0)]
	public Single FixedAcidity { get; set; }

    	[LoadColumn(1)]
	public Single VolatileAcidity { get; set; }

    	[LoadColumn(2)]
	public Single CitricAcid { get; set; }

    	[LoadColumn(3)]
	public Single ResidualSugar { get; set; }
    
	[LoadColumn(4)]
	public Single Chlorides { get; set; }
    
	[LoadColumn(5)]
	public Single FreeSulfurDioxide { get; set; }

    	[LoadColumn(6)]
	public Single TotalSulfurDioxide { get; set; }

    	[LoadColumn(7)]
	public Single Density { get; set; }
    
	[LoadColumn(8)]
	public Single Ph { get; set; }
    
	[LoadColumn(9)]
	public Single Sulphates { get; set; }
    
	[LoadColumn(10)]
	public Single Alcohol { get; set; }
      
	[LoadColumn(11)]
	public bool Quality { get; set; }
    };
}
