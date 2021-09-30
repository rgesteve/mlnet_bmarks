#include "daal.h"

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
        void* betaPtr, void* xtyPtr, void* xtxPtr, void* standardErrorsPtr);
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
void ridgeRegressionOnlineFinalizeTemplate(FPType * featuresPtr, FPType * labelsPtr, int nRows, int nColumns, float l2Reg, byte * partialResultPtr, int partialResultSize,
    FPType * betaPtr, FPType * xtyPtr, FPType * xtxPtr, FPType * standardErrorsPtr)
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
            if (j < i)
            {
                // Extend upper triangle of matrix
                xtx[j * nBetas + i] = xtx[i * nBetas + j];
            }
            else
            {
                // Add L2 regularization for future compute of inverted matrix X'X + L2 * E
                xtx[i * nBetas + j] += l2Reg;
            }
        }
    }

    // printf("%s\n", "X'X + L2 * I:");
    // for (size_t i = 0; i < nBetas; ++i)
    // {
    //     for (size_t j = 0; j < nBetas; ++j)
    //     {
    //         printf("%f ", xtx[i * nBetas + j]);
    //     }
    //     printf("\n");
    // }

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

    // Compute inverse of X'X + L2 * E
    svd::Batch<FPType> svdAlgorithm;
    svdAlgorithm.input.set(svd::data, xtxTable);
    svdAlgorithm.compute();

    svd::ResultPtr svdRes = svdAlgorithm.getResult();
    NumericTablePtr dTable = svdRes->get(svd::singularValues);
    NumericTablePtr vTable = svdRes->get(svd::rightSingularMatrix);
    NumericTablePtr uTable = svdRes->get(svd::leftSingularMatrix);
    BlockDescriptor<FPType> dBlock, vBlock, uBlock;
    dTable->getBlockOfRows(0, dTable->getNumberOfRows(), readWrite, dBlock);
    vTable->getBlockOfRows(0, vTable->getNumberOfRows(), readWrite, vBlock);
    uTable->getBlockOfRows(0, uTable->getNumberOfRows(), readWrite, uBlock);
    FPType * d = dBlock.getBlockPtr();
    FPType * v = vBlock.getBlockPtr();
    FPType * u = uBlock.getBlockPtr();

    // printf("%s\n", "U:");
    // for (size_t i = 0; i < nBetas; ++i)
    // {
    //     for (size_t j = 0; j < nBetas; ++j)
    //     {
    //         printf("%.12f", u[i * nBetas + j]);
    //         if (j < nBetas - 1)
    //         {
    //             printf(",");
    //         }
    //     }
    //     printf("\n");
    // }

    // printf("%s\n", "S:");
    // for (size_t i = 0; i < nBetas; ++i)
    // {
    //     printf("%.12f", d[i]);
    //     if (i < nBetas - 1)
    //     {
    //         printf(",");
    //     }
    // }
    // printf("\n");

    // printf("%s\n", "Vh:");
    // for (size_t i = 0; i < nBetas; ++i)
    // {
    //     for (size_t j = 0; j < nBetas; ++j)
    //     {
    //         printf("%.12f", v[i * nBetas + j]);
    //         if (j < nBetas - 1)
    //         {
    //             printf(",");
    //         }
    //     }
    //     printf("\n");
    // }

    for (size_t i = 0; i < nBetas; ++i)
    {
        for (size_t j = 0; j <= i; ++j)
        {
            FPType swap = v[i * nBetas + j];
            v[i * nBetas + j] = v[j * nBetas + i];
            v[j * nBetas + i] = swap;
        }
    }

    FPType * intermediate = new FPType[nBetas * nBetas];
    for (size_t i = 0; i < nBetas; ++i)
    {
        for (size_t j = 0; j < nBetas; ++j)
        {
            intermediate[i * nBetas + j] = v[i * nBetas + j] / d[j];
        }
    }
    // printf("%s\n", "intermediate:");
    // for (size_t i = 0; i < nBetas; ++i)
    // {
    //     for (size_t j = 0; j < nBetas; ++j)
    //     {
    //         printf("%f ", intermediate[i * nBetas + j]);
    //     }
    //     printf("\n");
    // }

    FPType * xtx_l2e_inv = new FPType[nBetas * nBetas];
    // for (size_t i = 0; i < nBetas; ++i)
    // {
    //     for (size_t j = 0; j <= i; ++j)
    //     {
    //         FPType swap = u[i * nBetas + j];
    //         u[i * nBetas + j] = u[j * nBetas + i];
    //         u[j * nBetas + i] = swap;
    //     }
    // }
    for (size_t i = 0; i < nBetas; ++i)
    {
        for (size_t j = 0; j < nBetas; ++j)
        {
            xtx_l2e_inv[i * nBetas + j] = 0;
            for (size_t k = 0; k < nBetas; ++k)
            {
                xtx_l2e_inv[i * nBetas + j] += intermediate[i * nBetas + k] * u[j * nBetas + k];
            }
        }
    }

    // printf("%s\n", "(X'X + L2 * I) ^ -1:");
    // for (size_t i = 0; i < nBetas; ++i)
    // {
    //     for (size_t j = 0; j < nBetas; ++j)
    //     {
    //         printf("%f ", xtx_l2e_inv[i * nBetas + j]);
    //     }
    //     printf("\n");
    // }

    for (size_t i = 0; i < nBetas; ++i)
    {
        for (size_t j = 0; j < nBetas; ++j)
        {
            intermediate[i * nBetas + j] = 0;
            for (size_t k = 0; k < nBetas; ++k)
            {
                intermediate[i * nBetas + j] += xtx_l2e_inv[i * nBetas + k] * xtx[k * nBetas + j];
            }
        }
    }

    // note: intercept located in end of betas in oneDAL and start in ML.NET
    standardErrorsPtr[0] = 0;
    for (size_t k = 0; k < nBetas; ++k)
    {
        standardErrorsPtr[0] += intermediate[(nBetas - 1) * nBetas + k] * xtx_l2e_inv[k * nBetas + (nBetas - 1)];
    }
    for (size_t i = 0; i < nBetas - 1; ++i)
    {
        standardErrorsPtr[i + 1] = 0;
        for (size_t k = 0; k < nBetas; ++k)
        {
            standardErrorsPtr[i + 1] += intermediate[i * nBetas + k] * xtx_l2e_inv[k * nBetas + i];
        }
    }

    xtxTable->releaseBlockOfRows(xtxBlock);
    xtyTable->releaseBlockOfRows(xtyBlock);
    betaTable->releaseBlockOfRows(betaBlock);
    dTable->releaseBlockOfRows(dBlock);
    vTable->releaseBlockOfRows(vBlock);
    uTable->releaseBlockOfRows(uBlock);

    delete xtx_l2e_inv, intermediate;
}

ONEDAL_C_EXPORT int ridgeRegressionOnlineCompute(void * featuresPtr, void * labelsPtr, int nRows, int nColumns, float l2Reg, void * partialResultPtr, int partialResultSize)
{
    return ridgeRegressionOnlineComputeTemplate<double>((double *)featuresPtr, (double *)labelsPtr, nRows, nColumns, l2Reg, (byte *)partialResultPtr, partialResultSize);
}

ONEDAL_C_EXPORT void ridgeRegressionOnlineFinalize(void * featuresPtr, void * labelsPtr, int nRows, int nColumns, float l2Reg, void * partialResultPtr, int partialResultSize,
    void * betaPtr, void * xtyPtr, void * xtxPtr, void * standardErrorsPtr)
{
    ridgeRegressionOnlineFinalizeTemplate<double>((double *)featuresPtr, (double *)labelsPtr, nRows, nColumns, l2Reg, (byte *)partialResultPtr, partialResultSize,
        (double *)betaPtr, (double *)xtyPtr, (double *)xtxPtr, (double *)standardErrorsPtr);
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
