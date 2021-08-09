/* file: lin_reg_norm_eq_dense_batch.cpp */
/*******************************************************************************
* Copyright 2014-2021 Intel Corporation
*
* Licensed under the Apache License, Version FEATURES.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
*     http://www.apache.org/licenses/LICENSE-FEATURES.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*******************************************************************************/

#include "daal.h"
#include "service.h"
#include "iostream"
#include <fstream>
#include <chrono>


using namespace std;
using namespace daal;
using namespace daal::data_management;
using namespace daal::algorithms::decision_forest::regression;


const string trainDatasetFileName = "data/DATASET_train.csv";
const string testDatasetFileName  = "data/DATASET_test.csv";
const size_t nFeatures = FEATURES; /* Number of features in training and testing data sets */


void getMetrics(NumericTable * truthTable, NumericTable * predictionTable, float * metrics)
{
    size_t n = truthTable->getNumberOfRows();
    BlockDescriptor<float> truthBlock, predictionBlock;
    truthTable->getBlockOfRows(0, n, readWrite, truthBlock);
    predictionTable->getBlockOfRows(0, n, readWrite, predictionBlock);

    float * truth = truthBlock.getBlockPtr();
    float * prediction = predictionBlock.getBlockPtr();

    float mean = 0.0;
    for (size_t i = 0; i < n; ++i) mean += truth[i];
    mean /= n;

    float sqSum = 0.0;
    float mae = 0.0;
    float rmse = 0.0;
    for (size_t i = 0; i < n; ++i)
    {
        sqSum += pow(truth[i] - mean, 2);
        mae += abs(truth[i] - prediction[i]);
        rmse += pow(truth[i] - prediction[i], 2);
    }
    float r2 = 1 - rmse / sqSum;
    mae /= n;
    rmse /= n;
    rmse = pow(rmse, 0.5);

    metrics[0] = mae;
    metrics[1] = rmse;
    metrics[2] = r2;
}

training::ResultPtr trainModel();
void testModel(const training::ResultPtr & res);
void testModel1(const training::ResultPtr & res);
void loadData(const std::string & fileName, NumericTablePtr & pData, NumericTablePtr & pDependentVar);

int main(int argc, char * argv[])
{
    checkArguments(argc, argv, 2, &trainDatasetFileName, &testDatasetFileName);

    training::ResultPtr trainingResult = trainModel();
    testModel(trainingResult);
    testModel1(trainingResult);

    return 0;
}

training::ResultPtr trainModel()
{
    /* Create Numeric Tables for training data and dependent variables */
    NumericTablePtr trainData;
    NumericTablePtr trainDependentVariable;

    loadData(trainDatasetFileName, trainData, trainDependentVariable);


    training::Batch<float, training::hist> algorithm;

    /* Pass a training data set and dependent values to the algorithm */
    algorithm.input.set(training::data, trainData);
    algorithm.input.set(training::dependentVariable, trainDependentVariable);

    algorithm.parameter().nTrees           = 100;
    algorithm.parameter().varImportance    = daal::algorithms::decision_forest::training::MDA_Raw;
    algorithm.parameter().resultsToCompute = daal::algorithms::decision_forest::training::computeOutOfBagError
                                             | daal::algorithms::decision_forest::training::computeOutOfBagErrorPerObservation;
   	algorithm.parameter().observationsPerTreeFraction = 0.7;
   	algorithm.parameter().featuresPerNode = 1;
   	algorithm.parameter().minBinSize = 1;
   	algorithm.parameter().maxBins = 256;
   	algorithm.parameter().minObservationsInLeafNode = 1;
   	algorithm.parameter().maxLeafNodes = 2;

    /* Build the decision forest regression model */
    std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();
    algorithm.compute();
    std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::cout << "Training Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[s]" << std::endl;

    /* Retrieve the algorithm results */
    training::ResultPtr trainingResult = algorithm.getResult();
    // printNumericTable(trainingResult->get(training::variableImportance), "Variable importance results: ");
    // printNumericTable(trainingResult->get(training::outOfBagError), "OOB error: ");
    // printNumericTable(trainingResult->get(training::outOfBagErrorPerObservation), "OOB error per observation (first 10 rows):", 10);
    return trainingResult;
}

void testModel(const training::ResultPtr & trainingResult)
{
    /* Create Numeric Tables for testing data and ground truth values */
    NumericTablePtr testData;
    NumericTablePtr testGroundTruth;

    loadData(testDatasetFileName, testData, testGroundTruth);

    /* Create an algorithm object to predict values of decision forest regression */
    prediction::Batch<> algorithm;

    /* Pass a testing data set and the trained model to the algorithm */
    algorithm.input.set(prediction::data, testData);
    algorithm.input.set(prediction::model, trainingResult->get(training::model));

    /* Predict values of decision forest regression */
    std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();
    algorithm.compute();
 	std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::cout << "Training Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[s]" << std::endl;

    /* Retrieve the algorithm results */
    prediction::ResultPtr predictionResult = algorithm.getResult();
    printNumericTable(predictionResult->get(prediction::prediction), "Decision forest prediction results (first 10 rows):", 10);

    BlockDescriptor<float> block;
    predictionResult->get(prediction::prediction)->getBlockOfRows(0, testData->getNumberOfRows(), readOnly, block);
    float *array = block.getBlockPtr();

    float testingMetrics[3];
	NumericTablePtr trainingPredictions = predictionResult->get(prediction::prediction);

    getMetrics(testGroundTruth.get(), trainingPredictions.get(), testingMetrics);
    for (size_t i=0; i<3; i++)
    {
    	cout<<testingMetrics[i]<<std::endl;
    }

    // ofstream myfile ("higgs_test_prediction.csv");
    // if (myfile.is_open())
    // {
    //     for (size_t row = 0; row < testData->getNumberOfRows(); row++)
    //     {
    //       for (size_t col = 0; col < 1; col++)
    //       {
    //         myfile << array[row * 1 + col] << ",";
    //       }
    //       myfile << std::endl;
    //     }
    //     myfile.close();
    // }

    // printNumericTable(predictionResult->get(classifier::prediction::prediction), "Logistic regression prediction results (first 10 rows):", 10);
    // printNumericTable(testGroundTruth, "Ground truth (first 10 rows):", 10);
}


void testModel1(const training::ResultPtr & trainingResult)
{
    /* Create Numeric Tables for testing data and ground truth values */
    NumericTablePtr testData;
    NumericTablePtr testGroundTruth;

    loadData(trainDatasetFileName, testData, testGroundTruth);
    /* Create an algorithm object to predict values of decision forest regression */

    prediction::Batch<> algorithm;

    /* Pass a testing data set and the trained model to the algorithm */
    algorithm.input.set(prediction::data, testData);
    algorithm.input.set(prediction::model, trainingResult->get(training::model));

    /* Predict values of decision forest regression */
    algorithm.compute();

    /* Retrieve the algorithm results */
    prediction::ResultPtr predictionResult = algorithm.getResult();
    printNumericTable(predictionResult->get(prediction::prediction), "Decision forest prediction results (first 10 rows):", 10);

    BlockDescriptor<float> block;
    predictionResult->get(prediction::prediction)->getBlockOfRows(0, testData->getNumberOfRows(), readOnly, block);
    float *array = block.getBlockPtr();

    float testingMetrics[3];
	NumericTablePtr trainingPredictions = predictionResult->get(prediction::prediction);

    getMetrics(testGroundTruth.get(), trainingPredictions.get(), testingMetrics);
    for (size_t i=0; i<3; i++)
    {
    	cout<<testingMetrics[i]<<std::endl;
    }

    // ofstream myfile ("higgs_train_prediction.csv");
    // if (myfile.is_open())
    // {
    //     for (size_t row = 0; row < testData->getNumberOfRows(); row++)
    //     {
    //       for (size_t col = 0; col < 1; col++)
    //       {
    //         myfile << array[row * 1 + col] << ",";
    //       }
    //       myfile << std::endl;
    //     }
    //     myfile.close();
    // }

    // printNumericTable(predictionResult->get(classifier::prediction::prediction), "Logistic regression prediction results (first 10 rows):", 10);
    // printNumericTable(testGroundTruth, "Ground truth (first 10 rows):", 10);
}

void loadData(const std::string & fileName, NumericTablePtr & pData, NumericTablePtr & pDependentVar)
{
    /* Initialize FileDataSource<CSVFeatureManager> to retrieve the input data from a .csv file */
    // FileDataSource<CSVFeatureManager> trainDataSource(fileName, DataSource::notAllocateNumericTable, DataSource::doDictionaryFromContext);
    CsvDataSourceOptions csvOptions(CsvDataSourceOptions::allocateNumericTable | CsvDataSourceOptions::createDictionaryFromContext | CsvDataSourceOptions::parseHeader);
    FileDataSource<CSVFeatureManager> trainDataSource(trainDatasetFileName, csvOptions);

    /* Create Numeric Tables for training data and dependent variables */
    pData.reset(new HomogenNumericTable<>(nFeatures, 0, NumericTable::notAllocate));
    pDependentVar.reset(new HomogenNumericTable<>(1, 0, NumericTable::notAllocate));
    NumericTablePtr mergedData(new MergedNumericTable(pData, pDependentVar));

    /* Retrieve the data from input file */
    trainDataSource.loadDataBlock(mergedData.get());
}
