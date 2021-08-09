from os import system as cmd


def run_bench(dataset, algorithm):
    print(f'/// {algorithm} - {dataset}')
    clsf_algorithms = ['log_reg', 'random_forest_classification']
    with open(f'data/{dataset}_train.csv') as training_file:
        text = training_file.read().split('\n')[:-1]
        features = len(text[0].split(',')) - 1
        if algorithm in clsf_algorithms:
            max_class = 1
            for line in text[1:]:
                current_class = int(line.split(',')[-1])
                if current_class > max_class:
                    max_class = current_class
            n_classes = max_class + 1

    if algorithm in clsf_algorithms:
        cmd(f'sed -i -e "s/const size_t nClasses = N_CLASSES/const size_t nClasses = {n_classes}/" onedal_{algorithm}.cpp')

    cmd(f'sed -i -e "s/DATASET/{dataset}/" onedal_{algorithm}.cpp')
    cmd(f'sed -i -e "s/const size_t nFeatures = FEATURES/const size_t nFeatures = {features}/" onedal_{algorithm}.cpp')

    cmd(f'g++ onedal_{algorithm}.cpp -std=c++11 -I$CONDA_PREFIX/include -Iutils -L$CONDA_PREFIX/lib -ltbb -ltbbmalloc -lonedal_core -lonedal_thread')
    cmd('./a.out')

    if algorithm in clsf_algorithms:
        cmd(f'sed -i -e "s/const size_t nClasses = {n_classes}/const size_t nClasses = N_CLASSES/" onedal_{algorithm}.cpp')
    cmd(f'sed -i -e "s/{dataset}/DATASET/" onedal_{algorithm}.cpp')
    cmd(f'sed -i -e "s/const size_t nFeatures = {features}/const size_t nFeatures = FEATURES/" onedal_{algorithm}.cpp')


if __name__ == '__main__':
    run_bench('year_prediction_msd', 'lin_reg')
    run_bench('year_prediction_msd', 'random_forest_regression')
    run_bench('a9a', 'log_reg')
    run_bench('a9a', 'random_forest_classification')
