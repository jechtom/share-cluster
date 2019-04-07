import { serverUrl } from 'Config';

var serverUrlExpanded = serverUrl === "+" ? location.host : serverUrl;
var isSecured = serverUrl === "+" ? (location.protocol === "https") : false;

export const uri_ws = (isSecured ? "wss" : "ws") + "://" + serverUrlExpanded + "/admin/ws" 
export const uri_api = (isSecured ? "https" : "http") + "://" + serverUrlExpanded + "/admin/commands"
export const uri_project_home = "https://github.com/jechtom/share-cluster"

console.log("uri_ws: " + uri_ws);
console.log("uri_api: " + uri_api);