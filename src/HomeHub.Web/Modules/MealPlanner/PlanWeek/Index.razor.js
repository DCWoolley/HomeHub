export function getCurrentDay() {
    return (new Date().getDay() + 6) % 7;
}
