export default function Packages(state = {groups : []}, action) {
    switch (action.type) {
      case 'PACKAGES_CHANGED':
        return { groups: action.data.Groups };
      default:
        return state;
    }
  }