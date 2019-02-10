const HtmlWebPackPlugin = require("html-webpack-plugin");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
var path = require("path");
const devMode = process.env.NODE_ENV !== 'production'

module.exports = {
    output: {
      path: path.resolve(__dirname, "./../wwwroot/"),
      publicPath: "",
      filename: "[name].bundle.js"
    },
    devServer: {
      contentBase: path.resolve(__dirname, "./../wwwroot/"),
      openPage: 'webpack-dev-server/index.html'
    },
    entry: './src/app.js',
    module: {
      rules: [

        {
          test: /\.(js|jsx)$/,
          exclude: /node_modules/,
          use: {
            loader: "babel-loader"
          }
        },

        {
          test: /\.html$/,
          use: [
            {
              loader: "html-loader",
              options: { minimize: true }
            }
          ]
        },

        {
          test: /\.(sa|sc|c)ss$/,
          use: [
            {
              // // Adds CSS to the DOM by injecting a `<style>` tag
              //loader: 'style-loader'

              // separate files with styles
              loader: MiniCssExtractPlugin.loader
            },
            {
              // Interprets `@import` and `url()` like `import/require()` and will resolve them
              loader: 'css-loader'
            },
            {
              // Loader for webpack to process CSS with PostCSS
              loader: 'postcss-loader',
              options: {
                plugins: function () {
                  return [
                    require('autoprefixer')
                  ];
                }
              }
            },
            {
              // Loads a SASS/SCSS file and compiles it to CSS
              loader: 'sass-loader'
            }
          ]
        }

      ]
    },
    plugins: [
      new HtmlWebPackPlugin({
        template: "./src/index.html",
        filename: "./index.html"
      }),
      new MiniCssExtractPlugin({
        filename: devMode ? '[name].css' : '[name].[hash].css',
        chunkFilename: devMode ? '[id].css' : '[id].[hash].css',
      })
    ]
  };