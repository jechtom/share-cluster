export default function Packages(state = {packages : []}, action) {
    switch (action.type) {
      case 'PACKAGES_CHANGED':
        return { packages : action.data.Packages }
      default:
        return state;
    }
  }