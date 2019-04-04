import axios from "axios";
import { uri_api } from '../constants'

export const Post = (operation, data, success) => {
    var url = uri_api + "/" + operation;
    console.log("Post to: " + url)
    console.log(data)
    axios
        .post(url, data)
        .then(response => {
            console.log(response);
            if(success) success();
        }).catch(error => console.error(error));
}