export default function ExtractPackage(state = createInitialState(), action) {
    switch (action.type) {
      case 'EXTRACT_PACKAGE_FORM':
        return {
          ...createInitialState(),
          packageId: action.packageId,
          packageName: action.packageName,
          sizeFormatted: action.sizeFormatted
        };
      case 'EXTRACT_PACKAGE_FORM_CHANGE':
        return { ...state, [action.id]: action.value };
      case 'EXTRACT_PACKAGE_FORM_OK':
        console.log("Package create request has been accepted");
        return createInitialState();
      case 'EXTRACT_PACKAGE_FORM_SUBMITTING':
        return { 
          ...state, 
          is_sending: true,
          error_message: null
        };
      case 'EXTRACT_PACKAGE_FORM_ERROR':
        return { 
          ...state, 
          is_sending: false,
          error_message: action.message
        };
      default:
        return state;
    }
  }

const createInitialState = () => ({
  packageId: null,
  packageName: null,
  sizeFormatted: null,
  path: "",
  is_sending: false,
  error_message: null
})