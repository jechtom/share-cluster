import { push } from 'connected-react-router'
import { Post } from "../services/ApiClient";
import { CopyToClipboard } from "../services/Clipboard";

export function package_download(packageId) {
    return function() {
        Post("PACKAGE_DOWNLOAD", { packageId: packageId });
    }
}

export function package_download_stop(packageId) {
    return function() {
        Post("PACKAGE_DOWNLOAD_STOP", { packageId: packageId });
    }
}

export function package_verify(packageId) {
    return function() {
        Post("PACKAGE_VERIFY", { packageId: packageId });
    }
}

export const packages_search_change = (term) => ({
    type: 'PACKAGES_SEARCH_CHANGE',
    term: term
})

export const packages_search_reset = (term) => ({
    type: 'PACKAGES_SEARCH_RESET',
    term: term
})

export const packages_delete_modal = (package_id, package_name) => ({
    type: 'PACKAGES_DELETE_MODAL',
    package_id: package_id,
    package_name: package_name
})

export const packages_delete_cancel = () => ({
    type: 'PACKAGE_DELETE_CANCEL'
})

export function packages_delete(package_id) {
    return function(dispatch) {
        Post("PACKAGE_DELETE", { packageId: package_id }, () => dispatch(packages_delete_cancel()));
    }
}

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

export function create_package_form_without_group() {
    return function(dispatch) {
        dispatch(push("/packages/create"));
        dispatch({
            type: "CREATE_PACKAGE_FORM_WITHOUT_GROUP"
        });
    }
}

export const create_package_form_change = (id, value) => ({
    type: "CREATE_PACKAGE_FORM_CHANGE",
    id: id,
    value: value
})

export function create_package_form_ok() {
    return function(dispatch) {
        console.log("create_package_form_ok");
        dispatch(push("/packages"));
        dispatch({type: "CREATE_PACKAGE_FORM_OK"});
    };
}

export function create_package_form_error(message) {
    return function(dispatch) {
        dispatch({type: "CREATE_PACKAGE_FORM_ERROR", message: message});
    };
}

export function create_package_form_submit(data) {
    return function(dispatch) {
        dispatch({type: "CREATE_PACKAGE_FORM_SUBMITTING"});
        Post("CREATE_PACKAGE", data, 
            () => dispatch(create_package_form_ok()),
            (error) => dispatch(create_package_form_error(error))
        );
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

export function extract_package_form(packageId, packageName, sizeFormatted) {
    return function(dispatch) {
        dispatch(push("/packages/extract"));
        dispatch({
            type: "EXTRACT_PACKAGE_FORM",
            packageId: packageId, 
            packageName: packageName, 
            sizeFormatted: sizeFormatted
        });
    }
}

export const extract_package_form_change = (id, value) => ({
    type: "EXTRACT_PACKAGE_FORM_CHANGE",
    id: id,
    value: value
})

export function extract_package_form_ok() {
    return function(dispatch) {
        dispatch(push("/packages"));
        dispatch({type: "EXTRACT_PACKAGE_FORM_OK"});
    };
}

export function extract_package_form_error(message) {
    return function(dispatch) {
        dispatch({type: "EXTRACT_PACKAGE_FORM_ERROR", message: message});
    };
}

export function extract_package_form_submit(data) {
    return function(dispatch) {
        dispatch({type: "EXTRACT_PACKAGE_FORM_SUBMITTING"});
        Post("EXTRACT_PACKAGE", data, 
            () => dispatch(extract_package_form_ok()),
            (error) => dispatch(extract_package_form_error(error))
        );
    }
}