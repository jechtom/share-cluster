import { push } from 'connected-react-router'
import { Post } from "../services/ApiClient";
import { CopyToClipboard } from "../services/Clipboard";

export const package_delete = (packageId) => ({
    type: "COMMAND_API",
    operation: "PACKAGE_DELETE",
    data: { packageId: packageId }
})

export const package_download = (packageId) => ({
    type: "COMMAND_API",
    operation: "PACKAGE_DOWNLOAD",
    data: { packageId: packageId }
})

export const package_download_stop = (packageId) => ({
    type: "COMMAND_API",
    operation: "PACKAGE_DOWNLOAD_STOP",
    data: { packageId: packageId }
})

export const package_verify = (packageId) => ({
    type: "COMMAND_API",
    operation: "PACKAGE_VERIFY",
    data: { packageId: packageId }
})

export const packages_search_change = (term) => ({
    type: 'PACKAGES_SEARCH_CHANGE',
    term: term
})

export const packages_search_reset = (term) => ({
    type: 'PACKAGES_SEARCH_RESET',
    term: term
})

export function create_package_form_with_group(groupId, groupName) {
    return function(dispatch) {
        dispatch(push("/packages/create"));
        dispatch({
            type: "CREATE_PACKAGE_FORM_WITH_GROUP",
            group_name: groupName,
            group_id: groupId
        });
    }
}

export const create_package_form_change = (id, value) => ({
    type: "CREATE_PACKAGE_FORM_CHANGE",
    id: id,
    value: value
})

export const create_package_form_without_group = () => ({
    type: "CREATE_PACKAGE_FORM_WITHOUT_GROUP"
})

export const create_package_form_ok = () => ({
    type: "CREATE_PACKAGE_FORM_OK"
})

export function create_package_form_submit(data) {
    return function(dispatch) {
        dispatch(push("/packages"));
        Post("CREATE_PACKAGE", data, () => dispatch(create_package_form_ok()));
    }
}

export function tasks_dismiss_all() {
    return function() {
        Post("TASKS_DISMISS", {}, () => {});
    }
}

export function clipboard_copy(text) {
    return function() {
        CopyToClipboard(text);
    }
}