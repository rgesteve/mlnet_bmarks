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
using namespace daal::algorithms;
using namespace daal::data_management;
using namespace daal::algorithms::decision_forest::classification;

const string trainDatasetFileName = "data/DATASET_train.csv";
const string testDatasetFileName  = "data/DATASET_test.csv";
const size_t nFeatures = FEATURES; /* Number of features in training and testing data sets */
const size_t nClasses = N_CLASSES; /* Number of classes */


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

    /* Create an algorithm object to train the decision forest classification model */
    training::Batch<float, training::hist> algorithm(nClasses);

    /* Pass a training data set and dependent values to the algorithm */
    algorithm.input.set(classifier::training::data, trainData);
    algorithm.input.set(classifier::training::labels, trainDependentVariable);

    algorithm.parameter().nTrees           = 100;
    algorithm.parameter().varImportance    = algorithms::decision_forest::training::MDI;
    algorithm.parameter().resultsToCompute = algorithms::decision_forest::training::computeOutOfBagError;
    algorithm.parameter().observationsPerTreeFraction = 0.7;
    algorithm.parameter().featuresPerNode = 26;
    algorithm.parameter().minBinSize = 1;
    algorithm.parameter().maxBins = 256;
    algorithm.parameter().minObservationsInLeafNode = 1;
    algorithm.parameter().maxLeafNodes = 1000;

    /* Build the decision forest classification model */
    std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();
    algorithm.compute();
    std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::cout << "Training Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[s]" << std::endl;


    /* Retrieve the algorithm results */
    training::ResultPtr trainingResult = algorithm.getResult();
    printNumericTable(trainingResult->get(training::variableImportance), "Variable importance results: ");
    printNumericTable(trainingResult->get(training::outOfBagError), "OOB error: ");
    return trainingResult;
}

void testModel(const training::ResultPtr & trainingResult)
{
    /* Create Numeric Tables for testing data and ground truth values */
    NumericTablePtr testData;
    NumericTablePtr testGroundTruth;

    loadData(testDatasetFileName, testData, testGroundTruth);

    /* Create an algorithm object to predict values of decision forest classification */
    prediction::Batch<> algorithm(nClasses);

    /* Pass a testing data set and the trained model to the algorithm */
    algorithm.input.set(classifier::prediction::data, testData);
    algorithm.input.set(classifier::prediction::model, trainingResult->get(classifier::training::model));
    algorithm.parameter().votingMethod = prediction::weighted;
    algorithm.parameter().resultsToEvaluate |= static_cast<DAAL_UINT64>(classifier::computeClassProbabilities);
    /* Predict values of decision forest classification */
    std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();
    algorithm.compute();
    std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::cout << "testing Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[s]" << std::endl;

    /* Retrieve the algorithm results */
    classifier::prediction::ResultPtr predictionResult = algorithm.getResult();
    printNumericTable(predictionResult->get(classifier::prediction::probabilities), "Decision forest probabilities results (first 10 rows):", 10);

    BlockDescriptor<float> block;
    predictionResult->get(classifier::prediction::probabilities)->getBlockOfRows(0, testData->getNumberOfRows(), readOnly, block);
    float *array = block.getBlockPtr();

    ofstream myfile ("onedal_rf_cls_test_prediction.csv");
    if (myfile.is_open())
    {
        for (size_t row = 0; row < testData->getNumberOfRows(); row++)
        {
          for (size_t col = 0; col < 2; col++)
          {
            myfile << array[row * 2 + col] << ",";
          }
          myfile << std::endl;
        }
        myfile.close();
    }
}

void testModel1(const training::ResultPtr & trainingResult)
{
    /* Create Numeric Tables for testing data and ground truth values */
    NumericTablePtr testData;
    NumericTablePtr testGroundTruth;

    loadData(trainDatasetFileName, testData, testGroundTruth);
    /* Create an algorithm object to predict values of decision forest regression */

    /* Create an algorithm object to predict values of decision forest classification */
    prediction::Batch<> algorithm(nClasses);

    /* Pass a testing data set and the trained model to the algorithm */
    algorithm.input.set(classifier::prediction::data, testData);
    algorithm.input.set(classifier::prediction::model, trainingResult->get(classifier::training::model));
    algorithm.parameter().votingMethod = prediction::weighted;
    algorithm.parameter().resultsToEvaluate |= static_cast<DAAL_UINT64>(classifier::computeClassProbabilities);
    /* Predict values of decision forest classification */
    algorithm.compute();

    /* Retrieve the algorithm results */
    classifier::prediction::ResultPtr predictionResult = algorithm.getResult();

    BlockDescriptor<float> block;
    predictionResult->get(classifier::prediction::probabilities)->getBlockOfRows(0, testData->getNumberOfRows(), readOnly, block);
    float *array = block.getBlockPtr();


    ofstream myfile ("onedal_rf_cls_train_prediction.csv");
    if (myfile.is_open())
    {
        for (size_t row = 0; row < testData->getNumberOfRows(); row++)
        {
          for (size_t col = 0; col < 2; col++)
          {
            myfile << array[row * 2 + col] << ",";
          }
          myfile << std::endl;
        }
        myfile.close();
    }
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
