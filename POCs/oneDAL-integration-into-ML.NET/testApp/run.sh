#!/bin/bash

run_dataset () {
    LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$PWD:$PWD/../machinelearning/artifacts/bin/Native/x64.Release/ dotnet run $1 onedal
    LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$PWD:$PWD/../machinelearning/artifacts/bin/Native/x64.Release/ dotnet run $1 default
}

run_dataset sample
# run_dataset ktps_aug
# run_dataset year_prediction_msd
# run_dataset abalone
