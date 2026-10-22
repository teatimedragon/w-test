import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AdminPage } from './pages/AdminPage'
import { LandingPage } from './pages/LandingPage'
import { RegistrationPage } from './pages/RegistrationPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/registration/:eventId" element={<RegistrationPage />} />
        <Route path="/admin" element={<AdminPage />} />
        <Route path="*" element={<main className="page"><h1>Page not found</h1></main>} />
      </Routes>
    </BrowserRouter>
  )
}
