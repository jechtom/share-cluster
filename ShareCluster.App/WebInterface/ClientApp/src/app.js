import 'bootstrap';
import './styles/app.scss';
import App from "./components/App.jsx";

import React from 'react';
import { render } from 'react-dom'
import { createStore, applyMiddleware } from 'redux'
import ApiService from './ApiService';
import { Provider } from 'react-redux'
import rootReducer from './reducers'

import './fontawesome-loader'

const store = createStore(rootReducer, applyMiddleware(ApiService))

render(
  <Provider store={store}>
    <App />
  </Provider>,
  document.getElementById('root')
)