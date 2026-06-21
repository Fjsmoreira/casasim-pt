import { Building2, Phone, Mail } from 'lucide-react'

interface AgencyCardProps {
  name?: string
  logo?: string
  phone?: string
  email?: string
  source?: string
}

const DEFAULT_AGENCY = 'CasaSim'

export default function AgencyCard({ name, logo, phone, email, source }: AgencyCardProps) {
  const agencyName = name || source || DEFAULT_AGENCY
  const hasContact = phone || email

  return (
    <div className="mb-8 p-5 bg-white rounded-xl border border-gray-200 shadow-sm">
      <div className="flex items-start gap-4">
        {/* Logo placeholder */}
        <div className="shrink-0 w-14 h-14 rounded-lg bg-sky-100 flex items-center justify-center">
          {logo ? (
            <img src={logo} alt={agencyName} className="w-full h-full object-contain rounded-lg" />
          ) : (
            <Building2 className="h-7 w-7 text-sky-600" />
          )}
        </div>

        <div className="min-w-0 flex-1">
          <h3 className="text-base font-semibold text-gray-900">
            {agencyName}
          </h3>
          <p className="text-xs text-gray-500 mt-0.5">Agência Imobiliária</p>

          {hasContact && (
            <div className="mt-3 space-y-1.5">
              {phone && (
                <a
                  href={`tel:${phone}`}
                  className="flex items-center gap-2 text-sm text-gray-600 hover:text-sky-600 transition-colors"
                >
                  <Phone className="h-4 w-4 text-gray-400" />
                  {phone}
                </a>
              )}
              {email && (
                <a
                  href={`mailto:${email}`}
                  className="flex items-center gap-2 text-sm text-gray-600 hover:text-sky-600 transition-colors"
                >
                  <Mail className="h-4 w-4 text-gray-400" />
                  {email}
                </a>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
