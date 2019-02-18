import React, { Component } from "react";
import ReactDOM from "react-dom";
import Input from "../presentational/Input.jsx";
import Websocket from 'react-websocket';

class FormContainer extends Component {
  constructor() {
    super();
    this.state = {
      seo_title: ""
    };
    this.handleChange = this.handleChange.bind(this);
  }
  handleChange(event) {
    this.setState({ [event.target.id]: event.target.value });
  }

  handleData(data) {
    console.log(data);
    this.setState({ seo_title: data });
  }

  render() {
    const { seo_title } = this.state;
    return (
      <div>
        <h1>Hello</h1>
        <strong>{seo_title}</strong>
        <form id="article-form">
          <Input
            text="SEO title"
            label="seo_title"
            type="text"
            id="seo_title"
            value={seo_title}
            handleChange={this.handleChange}
          />
        </form>

        <Websocket url='ws://localhost:13978/ws'
              onMessage={this.handleData.bind(this)}/>

      </div>
    );
  }
}
export default FormContainer;