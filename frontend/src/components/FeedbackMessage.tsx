export interface Feedback {
  tone: "success" | "info" | "error";
  message: string;
}

interface FeedbackMessageProps {
  feedback: Feedback | null;
}

export function FeedbackMessage({ feedback }: FeedbackMessageProps) {
  if (!feedback) {
    return null;
  }

  return (
    <div
      className={`feedback feedback-${feedback.tone}`}
      role={feedback.tone === "error" ? "alert" : "status"}
    >
      <p>{feedback.message}</p>
    </div>
  );
}
