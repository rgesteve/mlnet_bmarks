#include "daal.h"
#include "data_management/data/internal/finiteness_checker.h"

#ifdef __cplusplus
#define ONEDAL_EXTERN_C extern "C"
#else
#define ONEDAL_EXTERN_C
#endif

#ifdef _MSC_VER
#define ONEDAL_EXPORT __declspec(dllexport)
#define ONEDAL_C_EXPORT ONEDAL_EXTERN_C __declspec(dllexport)
#else
#define ONEDAL_EXPORT
#define ONEDAL_C_EXPORT ONEDAL_EXTERN_C
#endif

using namespace std;
using namespace daal;
using namespace daal::algorithms;
using namespace daal::data_management;


template <typename FPType>
void randomForestRegression(FPType * trainData, FPType * trainLabel, FPType * testData, FPType * predictions, int nTrainRows, int nTestRows, int nColumns)
{
    NumericTablePtr trainDataTable(new HomogenNumericTable<FPType>(trainData, nColumns, nTrainRows));
    NumericTablePtr trainLabelTable(new HomogenNumericTable<FPType>(trainLabel, 1, nTrainRows));
    NumericTablePtr testDataTable(new HomogenNumericTable<FPType>(testData, nColumns, nTestRows));

    // Training
    decision_forest::regression::training::Batch<FPType, decision_forest::regression::training::hist> trainingAlgorithm;
    trainingAlgorithm.parameter().nTrees           = 100;
    trainingAlgorithm.parameter().varImportance    = decision_forest::training::MDA_Raw;
    trainingAlgorithm.parameter().resultsToCompute = decision_forest::training::computeOutOfBagError
                                             | decision_forest::training::computeOutOfBagErrorPerObservation;
   	trainingAlgorithm.parameter().observationsPerTreeFraction = 0.7;
   	trainingAlgorithm.parameter().featuresPerNode = 1;
   	trainingAlgorithm.parameter().minBinSize = 1;
   	trainingAlgorithm.parameter().maxBins = 256;
   	trainingAlgorithm.parameter().minObservationsInLeafNode = 1;
   	// trainingAlgorithm.parameter().maxLeafNodes = 2;
    trainingAlgorithm.input.set(decision_forest::regression::training::data, trainDataTable);
    trainingAlgorithm.input.set(decision_forest::regression::training::dependentVariable, trainLabelTable);
    trainingAlgorithm.compute();
    decision_forest::regression::training::ResultPtr trainingResult = trainingAlgorithm.getResult();

    // Prediction
    decision_forest::regression::prediction::Batch<FPType> predictionAlgorithm;
    predictionAlgorithm.input.set(decision_forest::regression::prediction::data, testDataTable);
    predictionAlgorithm.input.set(decision_forest::regression::prediction::model, trainingResult->get(decision_forest::regression::training::model));
    predictionAlgorithm.compute();
    decision_forest::regression::prediction::ResultPtr predictionResult = predictionAlgorithm.getResult();
    NumericTablePtr predictionsTable = predictionResult->get(decision_forest::regression::prediction::prediction);

    BlockDescriptor<FPType> predictionsBlock;
    predictionsTable->getBlockOfRows(0, nTestRows, readWrite, predictionsBlock);
    FPType * predsForCopy = predictionsBlock.getBlockPtr();
    for (int i = 0; i < nTestRows; ++i)
    {
        predictions[i] = predsForCopy[i];
    }
}

ONEDAL_C_EXPORT void randomForestRegressionDouble(void * trainData, void * trainLabel, void * testData, void * predictions, int nTrainRows, int nTestRows, int nColumns)
{
    randomForestRegression<double>((double *)trainData, (double *)trainLabel, (double *)testData, (double *)predictions, nTrainRows, nTestRows, nColumns);
}

ONEDAL_C_EXPORT void randomForestRegressionSingle(void * trainData, void * trainLabel, void * testData, void * predictions, int nTrainRows, int nTestRows, int nColumns)
{
    randomForestRegression<float>((float *)trainData, (float *)trainLabel, (float *)testData, (float *)predictions, nTrainRows, nTestRows, nColumns);
}

template <typename FPType>
void linearRegression(FPType * trainData, FPType * trainLabel, FPType * testData, FPType * predictions, int nTrainRows, int nTestRows, int nColumns)
{
    NumericTablePtr trainDataTable(new HomogenNumericTable<FPType>(trainData, nColumns, nTrainRows));
    NumericTablePtr trainLabelTable(new HomogenNumericTable<FPType>(trainLabel, 1, nTrainRows));
    NumericTablePtr testDataTable(new HomogenNumericTable<FPType>(testData, nColumns, nTestRows));

    // Training
    linear_regression::training::Batch<FPType> trainingAlgorithm;
    trainingAlgorithm.input.set(linear_regression::training::data, trainDataTable);
    trainingAlgorithm.input.set(linear_regression::training::dependentVariables, trainLabelTable);
    trainingAlgorithm.compute();
    linear_regression::training::ResultPtr trainingResult = trainingAlgorithm.getResult();

    // Prediction
    linear_regression::prediction::Batch<FPType> predictionAlgorithm;
    predictionAlgorithm.input.set(linear_regression::prediction::data, testDataTable);
    predictionAlgorithm.input.set(linear_regression::prediction::model, trainingResult->get(linear_regression::training::model));
    predictionAlgorithm.compute();
    linear_regression::prediction::ResultPtr predictionResult = predictionAlgorithm.getResult();
    NumericTablePtr predictionsTable = predictionResult->get(linear_regression::prediction::prediction);

    BlockDescriptor<FPType> predictionsBlock;
    predictionsTable->getBlockOfRows(0, nTestRows, readWrite, predictionsBlock);
    FPType * predsForCopy = predictionsBlock.getBlockPtr();
    for (int i = 0; i < nTestRows; ++i)
    {
        predictions[i] = predsForCopy[i];
    }
}

ONEDAL_C_EXPORT void linearRegressionDouble(void * trainData, void * trainLabel, void * testData, void * predictions, int nTrainRows, int nTestRows, int nColumns)
{
    linearRegression<double>((double *)trainData, (double *)trainLabel, (double *)testData, (double *)predictions, nTrainRows, nTestRows, nColumns);
}

ONEDAL_C_EXPORT void linearRegressionSingle(void * trainData, void * trainLabel, void * testData, void * predictions, int nTrainRows, int nTestRows, int nColumns)
{
    linearRegression<float>((float *)trainData, (float *)trainLabel, (float *)testData, (float *)predictions, nTrainRows, nTestRows, nColumns);
}

ONEDAL_C_EXPORT bool doubleCheckFiniteness(void * numbers, int nRows, int nColumns)
{
    NumericTablePtr table(new HomogenNumericTable<double>((double *)numbers, nColumns, nRows));
    return internal::allValuesAreFinite<double>(*table, false);
}
