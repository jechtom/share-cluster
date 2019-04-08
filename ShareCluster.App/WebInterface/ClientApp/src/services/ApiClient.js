import axios from "axios";
import { uri_api } from '../constants'

export const Post = (operation, data, success_callback, error_callback=null) => {

    const handleError = (error) => {
        var errorMessage;
        console.log(error.response.data);
        if (error.response && error.response.status === 400 && error.response.data.success === false) {
            errorMessage = error.response.data.errorMessage;
        } else if (error.response) {
            errorMessage = "HTTP status " + error.response.status;
        } else if (error.request) {
            errorMessage = error.request;
        } else {
            errorMessage = 'Error API: ' + error.message; 
        }

        console.error('Error API', errorMessage); 
        if(error_callback) error_callback(errorMessage);             
    }

    const handleSuccess = (response) => {
        console.log('Success API', response); 
        if(success_callback) success_callback(response); 
    }

    var url = uri_api + "/" + operation;
    console.log("Post to: " + url)
    console.log(data)
    axios
        .post(url, data)
        .then(response => handleSuccess(response))
        .catch(error => handleError(error));
}