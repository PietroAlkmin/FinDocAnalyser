const path = require('path');

module.exports = {
    mode: 'production',
    entry: './src/findoc-widget.ts',
    module: {
        rules: [
            {
                test: /\.ts$/,
                use: 'ts-loader',
                exclude: /node_modules/,
                sideEffects: true
            },
            {
                test: /\.css$/,
                use: ['style-loader', 'css-loader']
            }
        ]
    },
    resolve: {
        extensions: ['.ts', '.js', '.css']
    },
    output: {
        filename: 'findoc-widget.js',
        path: path.resolve(__dirname, 'dist'),
        library: {
            name: 'FinDocWidget',
            type: 'umd',
            export: 'default'
        },
        globalObject: 'this',
        clean: true
    },
    optimization: {
        usedExports: false,
        sideEffects: true
    },
    devtool: 'source-map'
};