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

export const create_package_form_change = (id, value) => ({
    type: "CREATE_PACKAGE_FORM_CHANGE",
    id: id,
    value: value
})

export const create_package_form_submit = (data) => ({
    type: "COMMAND_API",
    operation: "CREATE_PACKAGE",
    data: data,
    callback: "CREATE_PACKAGE_FORM_OK"
})