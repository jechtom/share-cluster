const HtmlWebPackPlugin = require("html-webpack-plugin");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");
var path = require("path");

module.exports = (env, options) => {
  // remark: for some reason env is undefined but options.mode is set
  console.log("Init webpack. Env: " + options.mode)
  const devServer = process.argv[1].indexOf('webpack-dev-server') !== -1;
  const devMode = options.mode !== 'production'

  return {
    output: {
      path: path.resolve(__dirname, "./../wwwroot/"),
      publicPath: "",
      filename: devMode ? "[name].bundle.js" : "[name].[hash].bundle.js"
    },
    devServer: {
      contentBase: path.resolve(__dirname, "./../wwwroot/"),
      openPage: 'webpack-dev-server/index.html'
    },
    entry: [
      './src/app.js'
    ],
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
        template: "./src/index.ejs",
        filename: "./index.html"
      }),
      new MiniCssExtractPlugin({
        filename: devMode ? '[name].css' : '[name].[hash].css',
        chunkFilename: devMode ? '[id].css' : '[id].[hash].css',
      })
    ],
    externals: {
      'Config': JSON.stringify({
         serverUrl: devServer ? "localhost:13978" : "+"
      })
    }
  };
};