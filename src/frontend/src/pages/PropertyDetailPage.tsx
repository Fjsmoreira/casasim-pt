import { useParams } from 'react-router-dom'

export default function PropertyDetailPage() {
  const { id } = useParams()

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-2xl font-bold text-gray-900 mb-4">Detalhes do Imóvel</h1>
      <p className="text-gray-500">ID: {id}</p>
      <p className="text-gray-500 mt-2">
        Os detalhes completos estarão disponíveis após a integração com o backend.
      </p>
    </div>
  )
}
