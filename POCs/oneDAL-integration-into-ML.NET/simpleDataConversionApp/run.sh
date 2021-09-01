#!/bin/bash
gcc -std=c++11 -fPIC -shared -o lib_oneDALWrapper.so -L$CONDA_PREFIX/lib/ -I$CONDA_PREFIX/include -lonedal_core -lonedal_thread onedal_wrapper.cpp

LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run sample regression LinearRegression onedal
LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run sample regression LinearRegression mlnet
LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run sample regression RandomForest onedal
LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run sample regression RandomForest mlnet
#
# LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run year_prediction_msd regression LinearRegression onedal
# LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run year_prediction_msd regression LinearRegression mlnet
# LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run year_prediction_msd regression RandomForest onedal
# LD_LIBRARY_PATH=$CONDA_PREFIX/lib:$LD_LIBRARY_PATH dotnet run year_prediction_msd regression RandomForest mlnet
