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
using namespace daal::algorithms::logistic_regression;


/* Input data set parameters */
const string trainDatasetFileName = "data/DATASET_train.csv";
const string testDatasetFileName  = "data/DATASET_test.csv";
const size_t nFeatures = FEATURES; /* Number of features in training and testing data sets */

/* Logistic regression training parameters */
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


    services::SharedPtr<optimization_solver::lbfgs::Batch<> > lbfgs(new optimization_solver::lbfgs::Batch<>());
    lbfgs->parameter.batchSize =  trainData->getNumberOfRows();
    lbfgs->parameter.correctionPairBatchSize =  trainData->getNumberOfRows();
    lbfgs->parameter.m =  1;
    lbfgs->parameter.L =  1;
    lbfgs->parameter.accuracyThreshold = 0;

    /* Create an algorithm object to train the logistic regression model */
    training::Batch<> algorithm(nClasses);
    algorithm.parameter().optimizationSolver = lbfgs;

    /* Pass a training data set and dependent values to the algorithm */
    algorithm.input.set(classifier::training::data, trainData);
    algorithm.input.set(classifier::training::labels, trainDependentVariable);

    /* Build the logistic regression model */
    std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();
    algorithm.compute();
    std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::cout << "Training Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[s]" << std::endl;


    printNumericTable(lbfgs->getResult()->get(optimization_solver::iterative_solver::nIterations),
                      "number of iterations LBFGS", 10);
    /* Retrieve the algorithm results */
    training::ResultPtr trainingResult     = algorithm.getResult();
    logistic_regression::ModelPtr modelptr = trainingResult->get(classifier::training::model);
    if (modelptr.get())
    {
        printNumericTable(modelptr->getBeta(), "Logistic Regression coefficients:");
    }
    else
    {
        std::cout << "Null model pointer" << std::endl;
    }
    return trainingResult;
}

void testModel(const training::ResultPtr & trainingResult)
{
    /* Create Numeric Tables for testing data and ground truth values */
    NumericTablePtr testData;
    NumericTablePtr testGroundTruth;

    loadData(testDatasetFileName, testData, testGroundTruth);

    /* Create an algorithm object to predict values of logistic regression */
    prediction::Batch<> algorithm(nClasses);
    algorithm.parameter().resultsToEvaluate |= static_cast<DAAL_UINT64>(classifier::computeClassProbabilities);

    /* Pass a testing data set and the trained model to the algorithm */
    algorithm.input.set(classifier::prediction::data, testData);
    algorithm.input.set(classifier::prediction::model, trainingResult->get(classifier::training::model));

    /* Predict values of logistic regression */
    std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();
    algorithm.compute();
    std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::cout << "Prediction Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[s]" << std::endl;

    /* Retrieve the algorithm results */
    classifier::prediction::ResultPtr predictionResult = algorithm.getResult();
    printNumericTable(predictionResult->get(classifier::prediction::probabilities),
                      "Logistic regression prediction probabilities (first 10 rows):", 10);

    BlockDescriptor<float> block;
    predictionResult->get(classifier::prediction::probabilities)->getBlockOfRows(0, testData->getNumberOfRows(), readOnly, block);
    float *array = block.getBlockPtr();

    ofstream myfile ("onedal_log_reg_test_prediction.csv");
    if (myfile.is_open())
    {
        for (size_t row = 0; row < testData->getNumberOfRows(); row++)
        {
          for (size_t col = 0; col < nClasses; col++)
          {
            myfile << array[row * nClasses + col] << ",";
          }
          myfile << std::endl;
        }
        myfile.close();
    }

    // printNumericTable(predictionResult->get(classifier::prediction::prediction), "Logistic regression prediction results (first 10 rows):", 10);
    // printNumericTable(testGroundTruth, "Ground truth (first 10 rows):", 10);
}


void testModel1(const training::ResultPtr & trainingResult)
{
    /* Create Numeric Tables for testing data and ground truth values */
    NumericTablePtr testData;
    NumericTablePtr testGroundTruth;

    loadData(trainDatasetFileName, testData, testGroundTruth);

    /* Create an algorithm object to predict values of logistic regression */
    prediction::Batch<> algorithm(nClasses);
    algorithm.parameter().resultsToEvaluate |= static_cast<DAAL_UINT64>(classifier::computeClassProbabilities);

    /* Pass a testing data set and the trained model to the algorithm */
    algorithm.input.set(classifier::prediction::data, testData);
    algorithm.input.set(classifier::prediction::model, trainingResult->get(classifier::training::model));

    /* Predict values of logistic regression */
    std::chrono::steady_clock::time_point begin = std::chrono::steady_clock::now();
    algorithm.compute();
    std::chrono::steady_clock::time_point end = std::chrono::steady_clock::now();
    std::cout << "Prediction Time difference = " << std::chrono::duration_cast<std::chrono::microseconds>(end - begin).count() << "[s]" << std::endl;

    /* Retrieve the algorithm results */
    classifier::prediction::ResultPtr predictionResult = algorithm.getResult();
    printNumericTable(predictionResult->get(classifier::prediction::probabilities),
                      "Logistic regression prediction probabilities (first 10 rows):", 10);

    BlockDescriptor<float> block;
    predictionResult->get(classifier::prediction::probabilities)->getBlockOfRows(0, testData->getNumberOfRows(), readOnly, block);
    float *array = block.getBlockPtr();

    ofstream myfile ("onedal_log_reg_train_prediction.csv");
    if (myfile.is_open())
    {
        for (size_t row = 0; row < testData->getNumberOfRows(); row++)
        {
          for (size_t col = 0; col < nClasses; col++)
          {
            myfile << array[row * nClasses + col] << ",";
          }
          myfile << std::endl;
        }
        myfile.close();
    }

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
