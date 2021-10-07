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

/* OneDal ML.NET interoperability wrapper
internal static class OneDal
{
    private const string OneDalLibPath = "_oneDALWrapper.so";

    [DllImport(OneDalLibPath, EntryPoint = "ridgeRegressionOnlineCompute")]
    public unsafe static extern long RidgeRegressionOnlineCompute(void* featuresPtr, void* labelsPtr, long nRows, long nColumns, float l2Reg, void* partialResultPtr, long partialResultSize);

    [DllImport(OneDalLibPath, EntryPoint = "ridgeRegressionOnlineFinalize")]
    public unsafe static extern void RidgeRegressionOnlineFinalize(void* featuresPtr, void* labelsPtr, long nRows, long nColumns, float l2Reg, void* partialResultPtr, long partialResultSize,
        void* betaPtr, void* xtyPtr, void* xtxPtr);
}
*/
template <typename FPType>
int ridgeRegressionOnlineComputeTemplate(FPType * featuresPtr, FPType * labelsPtr, int nRows, int nColumns, float l2Reg, byte * partialResultPtr, int partialResultSize)
{
    // Create input data tables
    NumericTablePtr featuresTable(new HomogenNumericTable<FPType>(featuresPtr, nColumns, nRows));
    NumericTablePtr labelsTable(new HomogenNumericTable<FPType>(labelsPtr, 1, nRows));
    FPType l2 = l2Reg;
    NumericTablePtr l2RegTable(new HomogenNumericTable<FPType>(&l2, 1, 1));

    // Set up and execute training
    ridge_regression::training::Online<FPType> trainingAlgorithm;
    trainingAlgorithm.parameter.ridgeParameters = l2RegTable;

    ridge_regression::training::PartialResultPtr pRes(new ridge_regression::training::PartialResult);
    if (partialResultSize != 0)
    {
        OutputDataArchive dataArch(partialResultPtr, partialResultSize);
        pRes->deserialize(dataArch);
        trainingAlgorithm.setPartialResult(pRes);
    }

    trainingAlgorithm.input.set(ridge_regression::training::data, featuresTable);
    trainingAlgorithm.input.set(ridge_regression::training::dependentVariables, labelsTable);
    trainingAlgorithm.compute();

    // Serialize partial result
    pRes = trainingAlgorithm.getPartialResult();
    InputDataArchive dataArch;
    pRes->serialize(dataArch);
    partialResultSize = dataArch.getSizeOfArchive();
    dataArch.copyArchiveToArray(partialResultPtr, (size_t)partialResultSize);

    return partialResultSize;
}

template <typename FPType>
void ridgeRegressionOnlineFinalizeTemplate(FPType * featuresPtr, FPType * labelsPtr, long long int nAllRows, int nRows, int nColumns, float l2Reg, byte * partialResultPtr, int partialResultSize,
    FPType * betaPtr, FPType * xtyPtr, FPType * xtxPtr)
{
    NumericTablePtr featuresTable(new HomogenNumericTable<FPType>(featuresPtr, nColumns, nRows));
    NumericTablePtr labelsTable(new HomogenNumericTable<FPType>(labelsPtr, 1, nRows));
    FPType l2 = l2Reg;
    NumericTablePtr l2RegTable(new HomogenNumericTable<FPType>(&l2, 1, 1));

    ridge_regression::training::Online<FPType> trainingAlgorithm;

    ridge_regression::training::PartialResultPtr pRes(new ridge_regression::training::PartialResult);
    if (partialResultSize != 0)
    {
        OutputDataArchive dataArch(partialResultPtr, partialResultSize);
        pRes->deserialize(dataArch);
        trainingAlgorithm.setPartialResult(pRes);
    }

    trainingAlgorithm.parameter.ridgeParameters = l2RegTable;

    trainingAlgorithm.input.set(ridge_regression::training::data, featuresTable);
    trainingAlgorithm.input.set(ridge_regression::training::dependentVariables, labelsTable);
    trainingAlgorithm.compute();
    trainingAlgorithm.finalizeCompute();

    ridge_regression::training::ResultPtr trainingResult = trainingAlgorithm.getResult();
    ridge_regression::ModelNormEq * model = static_cast<ridge_regression::ModelNormEq *>(trainingResult->get(ridge_regression::training::model).get());

    NumericTablePtr xtxTable = model->getXTXTable();
    const size_t nBetas = xtxTable->getNumberOfRows();
    BlockDescriptor<FPType> xtxBlock;
    xtxTable->getBlockOfRows(0, nBetas, readWrite, xtxBlock);
    FPType * xtx = xtxBlock.getBlockPtr();

    size_t offset = 0;
    for (size_t i = 0; i < nBetas; ++i)
    {
        for (size_t j = 0; j <= i; ++j)
        {
            xtxPtr[offset] = xtx[i * nBetas + j];
            offset++;
        }
    }
    offset = 0;
    for (size_t i = 0; i < nBetas; ++i)
    {
        xtxPtr[offset] += l2Reg * l2Reg * nAllRows;
        offset += i + 2;
    }

    NumericTablePtr xtyTable = model->getXTYTable();
    BlockDescriptor<FPType> xtyBlock;
    xtyTable->getBlockOfRows(0, xtyTable->getNumberOfRows(), readWrite, xtyBlock);
    FPType * xty = xtyBlock.getBlockPtr();
    for (size_t i = 0; i < nBetas; ++i)
    {
        xtyPtr[i] = xty[i];
    }

    NumericTablePtr betaTable = trainingResult->get(ridge_regression::training::model)->getBeta();
    BlockDescriptor<FPType> betaBlock;
    betaTable->getBlockOfRows(0, 1, readWrite, betaBlock);
    FPType * betaForCopy = betaBlock.getBlockPtr();
    for (size_t i = 0; i < nBetas; ++i)
    {
        betaPtr[i] = betaForCopy[i];
    }

    xtxTable->releaseBlockOfRows(xtxBlock);
    xtyTable->releaseBlockOfRows(xtyBlock);
    betaTable->releaseBlockOfRows(betaBlock);
}

ONEDAL_C_EXPORT int ridgeRegressionOnlineCompute(void * featuresPtr, void * labelsPtr, int nRows, int nColumns, float l2Reg, void * partialResultPtr, int partialResultSize)
{
    return ridgeRegressionOnlineComputeTemplate<double>((double *)featuresPtr, (double *)labelsPtr, nRows, nColumns, l2Reg, (byte *)partialResultPtr, partialResultSize);
}

ONEDAL_C_EXPORT void ridgeRegressionOnlineFinalize(void * featuresPtr, void * labelsPtr, long long int nAllRows, int nRows, int nColumns, float l2Reg, void * partialResultPtr, int partialResultSize,
    void * betaPtr, void * xtyPtr, void * xtxPtr)
{
    ridgeRegressionOnlineFinalizeTemplate<double>((double *)featuresPtr, (double *)labelsPtr, nAllRows, nRows, nColumns, l2Reg, (byte *)partialResultPtr, partialResultSize,
        (double *)betaPtr, (double *)xtyPtr, (double *)xtxPtr);
}

template <typename FPType>
void linearRegression(FPType * features, FPType * label, FPType * betas, int nRows, int nColumns)
{
    NumericTablePtr featuresTable(new HomogenNumericTable<FPType>(features, nColumns, nRows));
    NumericTablePtr labelsTable(new HomogenNumericTable<FPType>(label, 1, nRows));

    // Training
    linear_regression::training::Batch<FPType> trainingAlgorithm;
    trainingAlgorithm.input.set(linear_regression::training::data, featuresTable);
    trainingAlgorithm.input.set(linear_regression::training::dependentVariables, labelsTable);
    trainingAlgorithm.compute();
    linear_regression::training::ResultPtr trainingResult = trainingAlgorithm.getResult();

    // Betas copying
    NumericTablePtr betasTable = trainingResult->get(linear_regression::training::model)->getBeta();
    BlockDescriptor<FPType> betasBlock;
    betasTable->getBlockOfRows(0, 1, readWrite, betasBlock);
    FPType * betasForCopy = betasBlock.getBlockPtr();
    for (size_t i = 0; i < nColumns + 1; ++i)
    {
        betas[i] = betasForCopy[i];
    }
}

template <typename FPType>
void ridgeRegression(FPType * features, FPType * label, FPType * betas, int nRows, int nColumns, float l2Reg)
{
    NumericTablePtr featuresTable(new HomogenNumericTable<FPType>(features, nColumns, nRows));
    NumericTablePtr labelsTable(new HomogenNumericTable<FPType>(label, 1, nRows));
    FPType l2 = l2Reg;
    NumericTablePtr l2RegTable(new HomogenNumericTable<FPType>(&l2, 1, 1));

    // Training
    ridge_regression::training::Batch<FPType> trainingAlgorithm;
    trainingAlgorithm.parameter.ridgeParameters = l2RegTable;
    trainingAlgorithm.input.set(ridge_regression::training::data, featuresTable);
    trainingAlgorithm.input.set(ridge_regression::training::dependentVariables, labelsTable);
    trainingAlgorithm.compute();
    ridge_regression::training::ResultPtr trainingResult = trainingAlgorithm.getResult();

    // Betas copying
    NumericTablePtr betasTable = trainingResult->get(ridge_regression::training::model)->getBeta();
    BlockDescriptor<FPType> betasBlock;
    betasTable->getBlockOfRows(0, 1, readWrite, betasBlock);
    FPType * betasForCopy = betasBlock.getBlockPtr();
    for (size_t i = 0; i < nColumns + 1; ++i)
    {
        betas[i] = betasForCopy[i];
    }
}

ONEDAL_C_EXPORT void linearRegressionDouble(void * features, void * label, void * betas, int nRows, int nColumns)
{
    linearRegression<double>((double *)features, (double *)label, (double *)betas, nRows, nColumns);
}

ONEDAL_C_EXPORT void linearRegressionSingle(void * features, void * label, void * betas, int nRows, int nColumns)
{
    linearRegression<float>((float *)features, (float *)label, (float *)betas, nRows, nColumns);
}

ONEDAL_C_EXPORT void ridgeRegressionDouble(void * features, void * label, void * betas, int nRows, int nColumns, float l2Reg)
{
    ridgeRegression<double>((double *)features, (double *)label, (double *)betas, nRows, nColumns, l2Reg);
}

ONEDAL_C_EXPORT void ridgeRegressionSingle(void * features, void * label, void * betas, int nRows, int nColumns, float l2Reg)
{
    ridgeRegression<float>((float *)features, (float *)label, (float *)betas, nRows, nColumns, l2Reg);
}
